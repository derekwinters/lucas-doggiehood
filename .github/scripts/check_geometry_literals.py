#!/usr/bin/env python3
"""Geometry / tuning inline-literal check (issue #161).

**The rule** (docs/engineering/tech-stack.md): every geometry, layout, and
tuning value — sizes, offsets, margins, positions, durations, speeds, payouts —
is declared as a named constant, static field, or serialized field at the top
of its type (or a shared Core numbers class), never as a bare numeric literal in
a method body. It applies to Core and the Unity wiring layer, graybox included.

**This script** is a deliberately conservative, low-false-positive *backstop*
for the egregious case that motivated the rule (#159: `140f`/`16f`/`32f` inside
`OnGUI`) — not a substitute for the human standard, which stays absolute. It
flags an f-suffixed floating-point literal only when all of the following hold:

  * it sits in an executable (method-body) context — not a type-level field or
    `const` declaration, which is the compliant named form the rule wants;
  * its magnitude is at least `FLAG_MIN_MAGNITUDE` (3.0), so structural values
    (0/1/2 for identity, both-sides, and centering) and sub-unit fractions
    (anchors, colour channels, epsilons) are ignored;
  * it is not inside a comment or string literal.

Because the game tree predates the rule, the check **ratchets against a
committed baseline** (`geometry_literals_baseline.txt`): pre-existing literals
are recorded there and don't fail CI, while any *newly introduced* one does. The
baseline is the burn-down list — shrink it with `--update-baseline` as literals
get named. Line numbers are deliberately excluded from the baseline key so it
survives unrelated line shifts.

The two decisions — what counts as a violation (`scan_source`) and the ratchet
(`diff_baseline`) — are pure functions so they can be unit-tested without a
checkout; `main` wires them to the filesystem.
"""

import argparse
import os
import re
import sys
from collections import namedtuple

# Directories scanned, relative to the repo root. All game code lives here; the
# Editor-only assembly under Assets/Scripts/Unity/Editor is included by nesting.
SCAN_ROOTS = ("Assets/Scripts",)

# f-suffixed literals below this magnitude are treated as structural (identity,
# both-sides doubling, centering halves, anchors, colour channels, epsilons) and
# are not flagged. The motivating #159 literals (16f/32f/140f) are all above it.
FLAG_MIN_MAGNITUDE = 3.0

BASELINE_PATH = os.path.join(os.path.dirname(__file__), "geometry_literals_baseline.txt")

# An f/F-suffixed decimal float literal not embedded in an identifier. Hex
# (`0xFF`) never matches: its digits are followed by hex letters, not by `f`
# after the numeric part. `2f`, `2.5f`, `.5f`, `140F` all match.
_FLOAT_RE = re.compile(r"(?<![A-Za-z0-9_])(\d+\.?\d*|\.\d+)[fF](?![A-Za-z0-9_])")

# A member-declaration hint: a line carrying one of these keywords is a named
# constant / static / serialized field, i.e. the compliant form — never flagged
# even if some brace-tracking edge case misclassifies its context.
_DECL_HINT_RE = re.compile(r"\b(const|readonly|SerializeField)\b")

# Opening a brace on a line carrying one of these keywords starts a *type* body,
# whose direct children are member declarations rather than executable code.
_TYPE_KW_RE = re.compile(r"\b(class|struct|interface|enum|record)\b")

Finding = namedtuple("Finding", ("line", "literal", "value", "snippet"))


def _strip_comments_and_strings(text):
    """Return `text` with comment and string/char contents blanked, preserving
    newlines (so line numbers are stable) and braces outside strings (so brace
    tracking is accurate). Interpolated-string bodies are blanked wholesale."""
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if c == "/" and nxt == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                if text[i] == "\n":
                    out.append("\n")
                i += 1
            i += 2
            continue
        if c == "@" and nxt == '"':  # verbatim string: "" is an escaped quote
            i += 2
            while i < n:
                if text[i] == '"' and i + 1 < n and text[i + 1] == '"':
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                if text[i] == "\n":
                    out.append("\n")
                i += 1
            continue
        if c == '"':  # ordinary or interpolated string (blanked wholesale)
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    i += 2
                    continue
                if text[i] == "\n":
                    out.append("\n")
                i += 1
            i += 1
            continue
        if c == "'":  # char literal
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    i += 2
                    continue
                i += 1
            i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def _member_context_map(code):
    """Walk `code` once, returning a function `member_at(offset)` telling whether
    that offset sits directly in a *type body* (member-declaration context) plus
    whether its current declaration head carries a member-declaration keyword.

    Context is resolved by position, not per line, so a literal sharing a line
    with the brace that changes context (e.g. `void M() { var w = 777f; }`) is
    classified correctly. A brace opens a *type* frame when its declaration head
    names a type, or when it is a field/property initializer (`=` present) inside
    an existing member context — so a named field's braced initializer stays
    member context, while a method or nested-scope body becomes executable.
    """
    events = []  # (offset, member_context, head_has_decl) sampled at each char
    stack = []
    head = []
    for i, ch in enumerate(code):
        member = bool(stack) and stack[-1] == "type"
        head_str = "".join(head)
        events.append((member, _DECL_HINT_RE.search(head_str) is not None))
        if ch == "{":
            if _TYPE_KW_RE.search(head_str):
                stack.append("type")
            elif member and "=" in head_str:
                stack.append("type")
            else:
                stack.append("block")
            head = []
        elif ch == "}":
            if stack:
                stack.pop()
            head = []
        elif ch == ";":
            head = []
        else:
            head.append(ch)
    return events


def scan_source(text):
    """Return the list of geometry/tuning-literal `Finding`s in one C# source.

    A finding is an f-suffixed float literal of magnitude >= FLAG_MIN_MAGNITUDE
    that appears in an executable context — its enclosing brace body is not a
    type body, and its declaration head carries no member-declaration keyword
    (`const`/`readonly`/`SerializeField`).
    """
    code = _strip_comments_and_strings(text)
    events = _member_context_map(code)
    lines = text.split("\n")
    findings = []
    for match in _FLOAT_RE.finditer(code):
        value = float(match.group(1))
        if abs(value) < FLAG_MIN_MAGNITUDE:
            continue
        member, head_has_decl = events[match.start()]
        if member or head_has_decl:
            continue
        lineno = code.count("\n", 0, match.start()) + 1
        snippet = lines[lineno - 1].strip() if lineno - 1 < len(lines) else ""
        findings.append(
            Finding(
                line=lineno,
                literal=match.group(0),
                value=value,
                snippet=snippet,
            )
        )
    return findings


def key_for(relpath, finding):
    """A baseline key that ignores line numbers so it survives line shifts."""
    snippet = re.sub(r"\s+", " ", finding.snippet).strip()
    return "{}\t{}\t{}".format(relpath.replace(os.sep, "/"), finding.literal, snippet)


def diff_baseline(current_keys, baseline_keys):
    """Split against the baseline. Returns (new, fixed):

    * `new`   — keys present now but absent from the baseline (fail the gate).
    * `fixed` — baseline keys no longer present (prune candidates; never fail).
    """
    current = set(current_keys)
    baseline = set(baseline_keys)
    new = sorted(current - baseline)
    fixed = sorted(baseline - current)
    return new, fixed


def iter_source_files(roots):
    for root in roots:
        for dirpath, _dirnames, filenames in os.walk(root):
            for name in sorted(filenames):
                if name.endswith(".cs"):
                    yield os.path.join(dirpath, name)


def collect_current_keys(repo_root, roots=SCAN_ROOTS):
    keys = set()
    for path in iter_source_files([os.path.join(repo_root, r) for r in roots]):
        with open(path, "r", encoding="utf-8") as handle:
            text = handle.read()
        relpath = os.path.relpath(path, repo_root)
        for finding in scan_source(text):
            keys.add(key_for(relpath, finding))
    return keys


def load_baseline(path=BASELINE_PATH):
    if not os.path.exists(path):
        return set()
    with open(path, "r", encoding="utf-8") as handle:
        return {
            line.rstrip("\n")
            for line in handle
            if line.strip() and not line.startswith("#")
        }


def write_baseline(keys, path=BASELINE_PATH):
    header = (
        "# Geometry/tuning inline-literal baseline (issue #161).\n"
        "# Pre-existing literals the ratchet tolerates; NEW ones fail CI.\n"
        "# Burn this down by naming the constants; regenerate with:\n"
        "#   python3 .github/scripts/check_geometry_literals.py --update-baseline\n"
        "# Format: <relpath>\\t<literal>\\t<normalized-snippet>\n"
    )
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(header)
        for key in sorted(keys):
            handle.write(key + "\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        default=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        help="Repository root to scan (defaults to this repo).",
    )
    parser.add_argument(
        "--update-baseline",
        action="store_true",
        help="Rewrite the baseline from the current tree instead of checking.",
    )
    args = parser.parse_args(argv)

    current = collect_current_keys(args.repo_root)

    if args.update_baseline:
        write_baseline(current)
        print("Wrote baseline with {} entr{}.".format(
            len(current), "y" if len(current) == 1 else "ies"))
        return 0

    baseline = load_baseline()
    new, fixed = diff_baseline(current, baseline)

    if fixed:
        print(
            "Note: {} baselined literal(s) are gone — prune the baseline with "
            "--update-baseline.".format(len(fixed))
        )

    if new:
        print(
            "\nGeometry/tuning rule (#161): {} new inline literal(s) found. Move "
            "each to a named constant/static/serialized field at the top of its "
            "type (see docs/engineering/tech-stack.md):\n".format(len(new))
        )
        for key in new:
            relpath, literal, snippet = key.split("\t", 2)
            print("  {}: {}  |  {}".format(relpath, literal, snippet))
        return 1

    print("Geometry/tuning literal check passed ({} baselined).".format(len(baseline)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
