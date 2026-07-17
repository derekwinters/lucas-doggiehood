# AI Issue-Management Pipeline

*Issue: [#191](https://github.com/derekwinters/lucas-doggiehood/issues/191)*

A label-driven pipeline moves issues from raw idea → analysis → Derek's
approval → nightly development, driven by scheduled Claude Code routines plus
one deterministic dashboard workflow.

## The model

**Labels are the state machine. Comments are Derek's control surface. A
"gatekeeper" translates comments into label moves.** Only the repo owner's
comments (`derekwinters`) are honored — the bad-actor gate. Everyone else's
`/commands` are ignored.

### States (labels)

| Label | Set by | Meaning |
| - | - | - |
| *(none)* | anyone | Raw idea. Ignored by the AI; shows in the dashboard intake. |
| `ai-triage` | gatekeeper (on `/admit`) | Admitted; queued for analysis. |
| `pending-approval` | analysis | Bug diagnosis / spec-covered plan posted; awaiting `/approve`. |
| `needs-clarification` | analysis | A clearly-stated question is on the issue; awaiting an answer. |
| `ready-for-work` | gatekeeper (on `/approve`) | Approved + milestone set; in the dev queue. |
| `in-progress` | dev | A nightly dev run picked it up / opened its PR. |
| `parked` | gatekeeper (on `/park`) | Hidden from every routine and the dashboard, any stage, indefinitely. |
| `dashboard` | one-time | Marks the dashboard issue ([#193](https://github.com/derekwinters/lucas-doggiehood/issues/193)); hard-excluded everywhere. |

`type:epic` issues are excluded from admit/dev throughout.

### Commands

Comment on any issue (prose around the command is fine — only the owner's
commands act):

| Command | Effect |
| - | - |
| `/admit` | Pull a raw idea into AI analysis (`ai-triage`). |
| `/approve` | Accept the analysis → `ready-for-work`, set the proposed (or `/milestone`-overridden) milestone. |
| `/revise <notes>` | Send back to analysis with feedback (re-add `ai-triage`). |
| `/redo` | Discard the analysis and start it over. |
| `/propose` | Authorize analysis to draft the missing design/wireframe as a marked PROPOSAL. |
| `/park` / `/unpark` | Hide from the pipeline / bring it back. |
| `/milestone <name>` | Override the milestone (`04`, a title fragment, or the full title). |
| `/focus <name>` | Set the active milestone for nightly development. |

Every AI hand-back comment ends with the context-appropriate "Your move" menu.

### The bad-actor gate and idempotency

The gatekeeper honors a command only if the comment's author is the repo owner.
Processed comments are watermarked with a 👀 reaction so re-running a routine
never double-applies a command. Both rules are enforced by the deterministic
parser (`.claude/skills/pipeline-gatekeeper/parse_commands.py`), not by model
judgment.

## Where `/focus` is stored

The active nightly-development milestone lives in a hidden marker on the
**first line of the dashboard issue (#193) body**:

```
<!-- pipeline-focus: 04 - Quests & Economy -->
```

This is the single source of truth shared by the gatekeeper (writes it on
`/focus`), `pipeline-dev` (reads it to pick the queue), and the dashboard
renderer (reads it, displays it, and re-emits it). It was chosen over a
committed state file (no routine needs to push a commit just to record focus)
and over a separate issue (the value sits next to where it's shown). If the
marker is absent, focus defaults to the lowest-numbered milestone with open
`ready-for-work` issues.

## Routines and the dashboard workflow

The gatekeeper runs first in each AI routine so downstream stages see fresh
labels.

| Time (CT / UTC) | Runner | Does |
| - | - | - |
| 7:00 AM / `0 12 * * *` | AI routine | gatekeeper → analysis |
| 6:00 PM / `0 23 * * *` | AI routine | gatekeeper (review refresh) |
| 1:00 AM / `0 6 * * *` | AI routine | gatekeeper → dev |
| 13:00, 00:00, 07:00 UTC | **Actions workflow** | dashboard render (`.github/workflows/dashboard.yml`) |

Fixed-UTC cron drifts one hour across US daylight-saving changes — accepted and
noted here rather than worked around.

**Why the dashboard is a workflow, not an AI step.** The dashboard body is a
pure function of repo state, so it is rendered by a deterministic script on a
GitHub Actions schedule — no model in the loop. That is cheaper, byte-stable,
and authenticates its headless PATCH with the built-in `GITHUB_TOKEN`, so no
extra secret is needed. The workflow runs ~1 hour after each AI routine so it
reflects the gatekeeper's label moves. (The original epic folded the dashboard
into each AI routine; this pivot supersedes that.)

## Stage behavior

### Analysis (`pipeline-analysis`)

Digs into every `ai-triage` issue in parallel and routes it to await Derek —
**never inventing design**:

- **Bug** → root-cause diagnosis + fix approach → `pending-approval` (adds
  `type:bug`).
- **Spec-covered feature** → implementation plan + a milestone proposed by
  matching **live milestone descriptions** → `pending-approval`.
- **Feature needing a new design call or a UI wireframe** (CLAUDE.md rule #8) →
  **stops and asks** with a concrete `❓ Needs from Derek/Lucas:` question →
  `needs-clarification`.
- **`/propose` set** → authorized to draft the design as a marked PROPOSAL →
  `pending-approval`.

Dependencies are recorded as first-class GitHub relationships — sub-issues for
decomposition, `Blocked by: #N` for hard peer dependencies, `Depends on: #N`
for soft sibling ordering.

### Development (`pipeline-dev`)

A serial nightly builder wrapping the `doggiehood-dev` agent. It builds the
eligible set — `ready-for-work` **and** in the focus milestone **and** all hard
blockers closed/merged **and** not `parked` **and** no open PR — in topological
order (dependencies first, then issue number), up to a nightly cap (**3** to
start). Each issue is built onto one shared branch; a failing issue's commits
are dropped so the branch stays green. It opens **one combined PR** (title = the
lead change's Conventional line; body = raw Conventional lines per issue for a
granular release-please changelog) and marks each built issue `in-progress`. It
**never merges and never closes** — Derek reviews and merges; PR-babysitting
keeps CI green.

### Dashboard (`pipeline-dashboard` + `dashboard.yml`)

Read-only. `render_dashboard.py` recomputes live state and rewrites **#193** in
place: focus-milestone pie (green done / yellow ready-for-work / red
remaining), the focus ready-for-work queue, "Your move" counts, PRs (release-
please separated), intake, pending-approval, needs-clarification, other-
milestone progress, and the command reference. It excludes #193 itself and
`parked` issues, and mutates nothing else.

## Skills

Each stage is a self-contained skill directory under `.claude/skills/`:

- `pipeline-gatekeeper/` — `SKILL.md` + `parse_commands.py` (deterministic
  command parser) + tests.
- `pipeline-analysis/` — `SKILL.md` (model-driven triage/design).
- `pipeline-dashboard/` — `SKILL.md` + `render_dashboard.py` + golden test;
  driven in production by `.github/workflows/dashboard.yml`.
- `pipeline-dev/` — `SKILL.md` + `select_queue.py` (eligibility + topological
  ordering) + tests.

The deterministic parts (command parsing, queue selection, dashboard render)
are scripted and unit-tested (run in CI via
[`pipeline-tests.yml`](ci-cd.md)); the model does only analysis, development,
and light acknowledgments.
