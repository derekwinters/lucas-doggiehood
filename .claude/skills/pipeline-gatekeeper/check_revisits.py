#!/usr/bin/env python3
"""Deterministic blocker auto-revisit check for the pipeline-gatekeeper skill.

An issue can be parked in ``needs-clarification`` *only because* it is
``Blocked by: #N`` — it needed a decision that lives in its blocker. Once every
such blocker is resolved (its blocker reaches ``ready-for-work``/``in-progress``
or is closed/merged), nothing was re-examining that issue: analysis only acts on
``ai-triage`` and the gatekeeper only acts on Derek's comments, so it stalled
indefinitely. This module is the missing automatic transition (issue #241): it
detects those now-unblocked ``needs-clarification`` issues and returns a revisit
action that swaps them back into the analysis queue.

Unlike ``parse_commands.py`` this is **not** a comment command — it is a
state-derived transition — so it lives alongside that parser as its own
function, not inside the comment parser. The gatekeeper runs it after processing
comment commands and applies each returned label swap, posting a short
auto-comment ending in the ``back-to-analysis`` menu.

It performs no GitHub I/O — the skill (SKILL.md) gathers the open-issue snapshot
via the GitHub MCP tools and applies each returned action.

Input schema (stdin / ``process``):
  {"issues": [{"number": 2, "labels": ["needs-clarification"],
               "body": "Blocked by: #1", "native_blocked_by": [1]}]}

  ``issues`` is the snapshot of **open** issues only (number, labels, body, and
  optional ``native_blocked_by``); a closed/merged blocker is therefore simply
  absent from the list and counts as resolved. ``native_blocked_by`` (issue
  #321) carries the issue's native GitHub issue-dependency hard blockers, which
  the gatekeeper SKILL reads from the dependencies API and unions with the
  structured ``Blocked by: #N`` text lines — so a blocker recorded only natively
  still gates (and clears) a revisit. There is no native soft-ordering form;
  ``Depends on:`` never gates a revisit.

Output schema (stdout / ``process``):
  {"revisits": [{"issue": 2, "add_labels": ["ai-triage"],
                 "remove_labels": ["needs-clarification"],
                 "blockers_resolved": [1], "menu": "back-to-analysis"}]}

A blocker is **resolved** when it is absent from the open snapshot (closed) or
carries ``ready-for-work`` / ``in-progress``. An issue is revisited only when it
is ``needs-clarification``, not ``parked``, has at least one hard blocker (from a
structured ``Blocked by: #N`` line or a native GitHub relationship), and
**every** such blocker is resolved.

**Wireframe-blocker carve-out (#396).** A blocker carrying the
``type:wireframe`` label is the one exception to the ``ready-for-work`` /
``in-progress`` shortcut: it resolves **only when closed** (absent from the open
snapshot). A wireframe issue at ``ready-for-work`` is merely approved to go
*draft* the wireframe; its downstream is hard-gated on the wireframe being
distilled into ``docs/specs/ui/`` and closed (CLAUDE.md rule #8 /
``docs/engineering/ui-design-process.md``). Without the carve-out the blocker's
label never changes, so every sweep re-fires the same revisit and single-issue
triage keeps concluding "still blocked" — the infinite churn #396 fixes. All
other blockers keep the ``ready-for-work`` / ``in-progress`` semantics from #241.
"""

import json
import re
import sys

# A blocker counts as resolved once it reaches one of these states (or closes,
# which drops it from the open snapshot entirely).
RESOLVED_LABELS = {"ready-for-work", "in-progress"}

# Wireframe-producing blockers are the exception (#396): a `type:wireframe`
# blocker at `ready-for-work` only means "approved to go *draft* the wireframe,"
# not "the wireframe is an approved, distilled contract." Its downstream is
# hard-gated on the wireframe being CLOSED (its layout distilled into
# `docs/specs/ui/`, per CLAUDE.md rule #8 / docs/engineering/ui-design-process.md),
# so a wireframe blocker resolves ONLY when it is absent from the open snapshot
# (closed/merged) — the `RESOLVED_LABELS` shortcut does not apply to it.
WIREFRAME_LABEL = "type:wireframe"

# The label swap a revisit applies — back into the analysis queue, mirroring
# /revise and /redo. Kept here so the transition is declared, not inlined.
REVISIT_ADD = ["ai-triage"]
REVISIT_REMOVE = ["needs-clarification"]
REVISIT_MENU = "back-to-analysis"

# A STRUCTURED hard-blocker line: the keyword ``blocked by``, then a REQUIRED
# colon, then ``#N``, at line start (optionally behind a ``-``/``*`` list
# marker). The colon is what makes the line canonical — the same form
# select_queue.py, reconcile.py, and the dashboard parse. A prose mention
# without the colon (``blocked by #1`` mid-sentence) must NOT fire an automatic
# label move, so it is deliberately not matched here.
_BLOCKED_BY_LINE_RE = re.compile(
    r"(?im)^[ \t]*(?:[-*][ \t]+)?blocked by[ \t]*:[ \t]*(.+)$"
)
_HASH_NUM_RE = re.compile(r"#(\d+)")


def _structured_blockers(body):
    """Issue numbers named on structured ``Blocked by: #N`` lines in ``body``."""
    nums = []
    for m in _BLOCKED_BY_LINE_RE.finditer(body or ""):
        nums.extend(int(n) for n in _HASH_NUM_RE.findall(m.group(1)))
    return nums


def check_blocker_revisits(issues):
    """Return revisit actions for now-unblocked ``needs-clarification`` issues.

    ``issues`` is the snapshot of OPEN issues (each ``{number, labels, body}``).
    Returns a list of ``{"issue", "add_labels", "remove_labels",
    "blockers_resolved", "menu"}`` — one per issue that should swap back into
    the analysis queue.
    """
    open_nums = {i["number"] for i in issues}
    labels_by = {i["number"]: set(i.get("labels", [])) for i in issues}

    def resolved(blocker):
        if blocker not in open_nums:
            return True  # closed / merged -> gone from the open snapshot
        blocker_labels = labels_by[blocker]
        if WIREFRAME_LABEL in blocker_labels:
            # A wireframe blocker resolves ONLY when closed (absent above), never
            # merely at ready-for-work/in-progress (#396) — otherwise the
            # downstream churns in an infinite revisit loop while the wireframe
            # is still just approved-to-draft.
            return False
        return bool(blocker_labels & RESOLVED_LABELS)

    revisits = []
    for i in issues:
        labels = set(i.get("labels", []))
        if "needs-clarification" not in labels or "parked" in labels:
            continue
        # Hard blockers gating a revisit come from BOTH the structured
        # `Blocked by: #N` text lines and native GitHub issue-dependency
        # relationships (#321) — the gatekeeper SKILL populates the snapshot's
        # `native_blocked_by` from the dependencies API. An issue whose only
        # blocker was recorded natively (no text line) still revisits once that
        # blocker resolves. Soft `Depends on:` ordering has no native form and
        # never gates a revisit.
        blockers = set(_structured_blockers(i.get("body", "")))
        blockers |= {int(n) for n in i.get("native_blocked_by") or []}
        if not blockers:
            continue
        if all(resolved(b) for b in blockers):
            revisits.append({
                "issue": i["number"],
                "add_labels": list(REVISIT_ADD),
                "remove_labels": list(REVISIT_REMOVE),
                "blockers_resolved": sorted(blockers),
                "menu": REVISIT_MENU,
            })
    return revisits


def process(data):
    return {"revisits": check_blocker_revisits(data.get("issues", []))}


def main():
    data = json.load(sys.stdin)
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
