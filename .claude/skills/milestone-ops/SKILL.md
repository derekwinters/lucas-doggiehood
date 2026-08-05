---
name: milestone-ops
description: >
  List, close, reopen, or count open issues in GitHub **milestones** for
  Doggiehood, via the milestones REST API. Use whenever you need to manage a
  milestone — cutting a release, closing a finished milestone, reopening one,
  or checking whether a milestone still has open work — because the GitHub MCP
  server exposes no milestone CRUD at all. Invoke when asked to list, close,
  reopen, or inspect the open issues of a milestone.
---

# Milestone ops — milestone CRUD the MCP server doesn't have

The GitHub MCP server exposes **no milestone tools** — no list, close, reopen,
or "open issues in a milestone". So every milestone chore (finishing a release,
closing the shipped milestone, setting the next focus) otherwise means
rediscovering the raw REST calls by hand. This skill wraps them, mirroring
`.claude/skills/issue-blockers/` (built for the same reason: no MCP tool for a
GitHub object we routinely manage).

## The commands

`milestone_ops.py` (this skill's folder) wraps the API. `GITHUB_TOKEN` (or
`GH_TOKEN`) must be set — it already is in CI and web sessions.

```bash
SKILL=.claude/skills/milestone-ops/milestone_ops.py

# list every milestone: number, state, title, open/closed issue counts
python3 "$SKILL" list

# how many open issues remain in a milestone (title OR number)
python3 "$SKILL" open-issues v0.11        # or: open-issues 21

# close a milestone — REFUSES if open issues remain, unless --force
python3 "$SKILL" close v0.11
python3 "$SKILL" close v0.11 --force

# reopen a closed milestone
python3 "$SKILL" reopen v0.11

# a different repo (default is derekwinters/lucas-doggiehood)
python3 "$SKILL" list --repo owner/name
```

## The endpoints (and their gotchas)

- **List** — `GET /repos/{repo}/milestones?state=all&per_page=100`.
  `state=all` is required to see closed milestones; each item carries `number`,
  `state`, `title`, and the **`open_issues` / `closed_issues`** count fields the
  formatter prints.
- **Open issues** — `GET /repos/{repo}/issues?milestone={number}&state=open`.
  The pre-close safety check — don't close a milestone with open work.
- **Close / reopen** — `PATCH /repos/{repo}/milestones/{number}` with
  `{"state":"closed"}` or `{"state":"open"}`. There is no "close milestone"
  button in any tool we have.

**The `number` is not the version string.** The PATCH/close call is keyed by the
milestone's numeric `number` — e.g. `v0.11` was milestone number `21`. That's
exactly why `open-issues`, `close`, and `reopen` accept a **title** and resolve
it to a number against the `list` payload; a bare numeric argument passes
straight through unresolved.

**Close-guard.** `close` re-fetches the open-issue count and **refuses** (no
PATCH sent, exit 1) when any open issues remain, unless `--force` is passed.

## Proxy-auth note

In the web / Claude-Code-Remote environment, plain `curl` works for both the GET
and the PATCH against these endpoints — the agent proxy handles auth, so no
token wrangling is needed for a one-off by hand:

```bash
curl -s "https://api.github.com/repos/derekwinters/lucas-doggiehood/milestones?state=all"
```

The script uses `GITHUB_TOKEN`/`GH_TOKEN` explicitly so it also runs in CI where
the token is set. Either path reaches the same API.

## Shape (for maintenance)

Same split as the other deterministic skills: the URL/path/payload builders and
the title→number resolver / close-guard decision in `milestone_ops.py` are
**pure and unit-tested** (`tests/test_milestone_ops.py`); GitHub I/O lives only
in the `_api_request` edge and is not unit-tested. Run the tests with
`python3 -m unittest discover -s tests` from the skill folder.
