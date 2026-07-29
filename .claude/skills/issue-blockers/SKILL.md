---
name: issue-blockers
description: >
  Create, remove, or list **native** GitHub issue dependency (blocker)
  relationships for Doggiehood issues, via the issue-dependencies REST API.
  Use whenever one issue depends on / is blocked by another — instead of
  writing a prose "Blocked by #N" line, which is not allowed (CLAUDE.md rule
  #11) and gets flagged as drift by the reconcile sweep. Invoke when asked to
  make an issue depend on / block / be blocked by another, or to wire up a
  dependency before creating/approving work.
---

# Issue blockers — native dependencies, never prose

Doggiehood records "issue A is blocked by issue B" as a **native GitHub
issue-dependency relationship**, never as text in the issue body. Prose
dependency lines are banned (CLAUDE.md rule #11): the pipeline's reconcile
sweep reads native blockers (`reconcile.py::native_blocked_by`) and actively
**flags** prose-only dependency phrases as drift (`flag_prose_dep`, surfaced on
the dashboard). This skill is the write side that keeps every relationship on
the native path.

## The one command

`set_blocker.py` (this skill's folder) wraps the API. `GITHUB_TOKEN` (or
`GH_TOKEN`) must be set — it already is in CI and web sessions.

```bash
SKILL=.claude/skills/issue-blockers/set_blocker.py

# "#295 is blocked by #360"
python3 "$SKILL" 295 --blocked-by 360

# undo it
python3 "$SKILL" 295 --blocked-by 360 --remove

# what is #295 blocked by?
python3 "$SKILL" 295 --list

# a different repo
python3 "$SKILL" 295 --blocked-by 360 --repo owner/name
```

Read direction: `<blocked> --blocked-by <blocker>` mirrors the sentence
"**#blocked** is blocked by **#blocker**." Equivalently, #blocker *blocks*
#blocked; set it from the blocked side.

## Why a script and not raw curl

The issue-dependencies API is **asymmetric** and easy to get wrong by hand:

- **Read** — `GET /repos/{repo}/issues/{n}/dependencies/blocked_by` returns full
  issue objects, so a blocker's identity there is its `#number`.
- **Write** — `POST /repos/{repo}/issues/{n}/dependencies/blocked_by` takes a
  numeric `{"issue_id": <database id>}`, **not** the `#number`; `DELETE
  …/blocked_by/{issue_id}` likewise.

The script resolves the blocker's database `id` for you (one GET) and posts it,
so callers only ever deal in issue numbers.

## Conventions

- **Set the dependency at issue-creation time**, or as soon as the blocking
  issue exists — don't leave a "TODO wire this up" prose note in the meantime.
- **Never** write `Blocked by #N` / `Depends on #N` in an issue body or comment
  as the source of truth. If you find one, convert it to a native relationship
  with this skill and delete the prose line.
- The relationship is directional: adding "#A blocked by #B" automatically makes
  "#B blocking #A" — you only set one side.

## Shape (for maintenance)

Same split as the other deterministic skills: the URL/path/payload builders in
`set_blocker.py` are **pure and unit-tested** (`tests/test_set_blocker.py`);
GitHub I/O lives only in the `_api_request` edge and is not unit-tested. Run the
tests with `python3 -m unittest discover -s tests` from the skill folder.
