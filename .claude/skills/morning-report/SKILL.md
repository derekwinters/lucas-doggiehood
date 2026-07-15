---
name: morning-report
description: >
  Generate a morning status report for the derekwinters/lucas-doggiehood
  repository: issues awaiting Derek/Lucas attention, milestone progress
  with a recommended next issue, and a summary of open pull requests.
  Use when asked for a morning report, status update, daily summary, or
  "what's going on in the repo".
---

# Morning report

Produces a single Markdown report covering three sections, in this order.
Use the GitHub MCP tools (`mcp__github__*`) against
`derekwinters/lucas-doggiehood` for all data — don't guess or rely on
stale local knowledge, and don't include repos outside this one.

## 1. Issues that Need my Attention

Fetch open issues labeled `pending-approval` and open issues labeled
`needs-clarification` (two separate queries, e.g. `list_issues` filtered
by label, or `search_issues` with
`repo:derekwinters/lucas-doggiehood is:open is:issue label:pending-approval`).

For each issue, write a one-sentence summary from its title/body — don't
just repeat the title.

Render exactly this shape, omitting a subsection entirely if it has no
matching issues (don't print an empty table):

```
### Pending Approval

| Issue # | Issue Title | Summary |
| - | - | - |
| [#1](https://github.com/derekwinters/lucas-doggiehood/issues/1) | The Issue Title | One sentence summary |

### Needs Clarification

| Issue # | Issue Title | Summary |
| - | - | - |
| [#1](https://github.com/derekwinters/lucas-doggiehood/issues/1) | The Issue Title | One sentence summary |
```

If neither label has any open issues, write a single line: "No issues
pending approval or clarification." instead of the headers.

## 2. Milestone Progress

List milestones in build order (`00` → `08`, see
`docs/intro/conventions.md`), each with open/closed issue counts and a
rough percent complete. Skip milestones with zero issues. Keep it compact
— one line per milestone, not a table per milestone.

Then add a short **recommendation**: the single next issue to work,
following the project's build order (lowest milestone number with open
work, lowest issue number within it — see
`docs/intro/conventions.md` and `docs/engineering/agent-workflow.md`).
Name the issue number/title and one sentence on why it's next (e.g. it's
the lowest-numbered open issue in the earliest incomplete milestone, or
it's a blocker for other open issues). If the earliest open issue is in
`Direct Involvement Needed` or otherwise not agent-workable, say so and
recommend the next agent-workable issue instead, noting the human task is
still outstanding.

## 3. Pull Requests

List open pull requests except the `release-please` automation PR (its
branch is typically `release-please--branches--main` or its title starts
with `chore(main): release`). For each: PR number/title/link, a one-line
state summary (e.g. "CI passing, awaiting review", "CI failing on X",
"draft, WIP"), and whether it looks stale or blocked.

If there are no open PRs besides release-please, say so in one line.

## Output

Return the full report as Markdown text in the chat — this skill doesn't
commit or push anything. Don't wrap it in a code fence (the tables need to
render).
