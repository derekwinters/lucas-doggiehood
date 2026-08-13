#!/usr/bin/env python3
"""Mechanical issue gate — a `PreToolUse` hook for Doggiehood (issue #684).

Denies `Edit` / `Write` / `MultiEdit` against the project's code and build
configuration unless the session names an approved GitHub issue it is
building. Everything else — docs, `.claude/**`, issue filing, and every
read-only tool — is untouched, so triage and investigation still work.

Shape (mirrors `.claude/skills/pipeline-gatekeeper/parse_commands.py`): the
decision is a **pure function** of the hook event, the approved-issue signal,
and an injected label lookup. GitHub I/O lives only at the edge, in
`live_labels()`, using the same `GITHUB_TOKEN` auth pattern as
`.claude/skills/pipeline-gatekeeper/_github_api.py`.
"""

import collections
import json
import os
import sys

#: Write tools the gate fires on. Read-only tools are never gated.
GATED_TOOLS = ("Edit", "Write", "MultiEdit")

#: Repo-relative globs the gate protects. Each is a directory subtree, so a
#: path is gated when its first segment matches the glob's prefix.
GATED_GLOBS = ("Assets/**", "CoreTests/**", "ProjectSettings/**", "Packages/**")

#: The env var a pipeline/orchestration run sets to the issue it is building.
ENV_APPROVED_ISSUE = "DOGGIEHOOD_APPROVED_ISSUE"

#: Live label states that pass the gate. `ready-for-work` is where the
#: gatekeeper parks an approved issue; `in-progress` is where `pipeline-dev`
#: and `milestone-orchestration` move it as their FIRST action, before any
#: code is written. The pipeline's labels are mutually exclusive
#: (docs/engineering/issue-pipeline.md), so checking `ready-for-work` alone
#: would deny every automated build. `in-progress` is only ever reachable
#: *from* `ready-for-work`, so accepting both keeps the gate's intent: an
#: untriaged or `pending-approval` issue carries neither and is still denied.
APPROVED_LABELS = ("ready-for-work", "in-progress")

#: The CLAUDE.md rule this hook enforces, named in every denial.
RULE = "rule #13"

#: GitHub REST base + repo, matching the pipeline scripts' conventions.
API = "https://api.github.com"
REPO_DEFAULT = "derekwinters/lucas-doggiehood"

Decision = collections.namedtuple("Decision", "allowed reason")

_ALLOW = Decision(True, "")


def _default_root():
    """The project root: this file lives at `<root>/.claude/hooks/`."""
    return os.path.abspath(
        os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     os.pardir, os.pardir))


def _relative_path(path, project_root):
    """`path` relative to `project_root`, or None when it is outside it."""
    if not path:
        return None
    root = os.path.abspath(project_root or _default_root())
    absolute = os.path.normpath(
        path if os.path.isabs(path) else os.path.join(root, path))
    if absolute == root:
        return None
    prefix = root.rstrip(os.sep) + os.sep
    if not absolute.startswith(prefix):
        return None
    return absolute[len(prefix):].replace(os.sep, "/")


def _target_path(tool_input):
    return (tool_input or {}).get("file_path")


def gate_applies(tool_name, path, project_root=None):
    """True when this (tool, path) pair is inside the gate's scope."""
    if tool_name not in GATED_TOOLS:
        return False
    relative = _relative_path(path, project_root)
    if relative is None:
        return False
    return any(relative.split("/")[0] == glob.split("/")[0]
               for glob in GATED_GLOBS)


def _issue_number(approved_issue):
    """The issue number named by the signal, or None when absent/malformed."""
    text = (approved_issue or "").strip().lstrip("#")
    if not text.isdigit():
        return None
    number = int(text)
    return number if number > 0 else None


def decide(event, approved_issue, lookup, project_root=None):
    """Pure gate decision for one `PreToolUse` event.

    `lookup` is called at most once, only when the gate applies and the signal
    names a plausible issue — so an ungated edit costs no GitHub I/O.
    """
    tool_name = (event or {}).get("tool_name")
    path = _target_path((event or {}).get("tool_input"))
    if not gate_applies(tool_name, path, project_root):
        return _ALLOW

    number = _issue_number(approved_issue)
    if number is None:
        return Decision(False, (
            "Blocked by the Doggiehood issue gate (CLAUDE.md %s): this session "
            "has no approved issue, so it may not edit %s. Set "
            "%s=<issue number> for an issue that is already approved "
            "(`ready-for-work`) or being built (`in-progress`) — or stop and "
            "hand back so the work can be triaged and approved first. Docs, "
            "issue filing and read-only investigation are not blocked."
            % (RULE, ", ".join(GATED_GLOBS), ENV_APPROVED_ISSUE)))

    try:
        labels = lookup(number) if lookup else None
    except Exception:  # noqa: BLE001 - see the fail-open note below
        labels = None

    if labels is None:
        # FAIL-OPEN, deliberately: an unreachable API (outage, or a session
        # with no token) must not halt all development. The failure this gate
        # exists to catch — code written with no issue at all — is denied
        # above without any network call.
        return _ALLOW

    if not set(labels) & set(APPROVED_LABELS):
        return Decision(False, (
            "Blocked by the Doggiehood issue gate (CLAUDE.md %s): "
            "%s names issue #%d, but that issue is not approved for "
            "development — its live labels are neither `ready-for-work` nor "
            "`in-progress` (found: %s). Get the issue triaged and approved "
            "before editing %s."
            % (RULE, ENV_APPROVED_ISSUE, number,
               ", ".join(sorted(labels)) or "none", ", ".join(GATED_GLOBS))))

    return _ALLOW


def live_labels(number, repo=None, token=None):
    """The issue's current label names, or None when it can't be determined.

    The only GitHub I/O in this module (stdlib `urllib`, same auth pattern as
    `.claude/skills/pipeline-gatekeeper/_github_api.py`). A definitive "that
    issue has no approving label" is an empty set; "couldn't tell" is None,
    which the caller treats as fail-open.
    """
    import urllib.error
    import urllib.request

    token = token or os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        return None
    repo = repo or os.environ.get("DOGGIEHOOD_REPO") or REPO_DEFAULT
    request = urllib.request.Request(
        "%s/repos/%s/issues/%d" % (API, repo, number))
    request.add_header("Authorization", "Bearer %s" % token)
    request.add_header("Accept", "application/vnd.github+json")
    request.add_header("User-Agent", "doggiehood-issue-gate")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            issue = json.loads(response.read())
    except urllib.error.HTTPError as error:
        # A missing issue is a definitive answer: no approving label.
        return set() if error.code == 404 else None
    except Exception:  # noqa: BLE001 - network/parse trouble is "couldn't tell"
        return None
    return {label.get("name") for label in issue.get("labels") or []}


def main():
    try:
        event = json.load(sys.stdin)
    except (ValueError, OSError):
        # A gate that crashes the session is worse than one that misses an
        # edit: unparseable input is never treated as a denial.
        return 0
    decision = decide(event, os.environ.get(ENV_APPROVED_ISSUE), live_labels)
    if decision.allowed:
        return 0
    json.dump({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": "deny",
        "permissionDecisionReason": decision.reason,
    }}, sys.stdout)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
