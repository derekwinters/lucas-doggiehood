---
name: pipeline-analysis
description: >
  Dispatch triage across every admitted (`ai-triage`) Doggiehood issue: run
  the deterministic `select_triage.py` discovery script, then invoke the
  single-issue `triage-issue` skill once per eligible issue with bounded
  concurrency. Runs in the AM triage routine after the gatekeeper.
---

# Pipeline analysis (dispatcher)

Runs the **AM triage** routine (7 AM CT), after the gatekeeper. This skill is
now a thin dispatcher (issue #320): it discovers *which* issues need triage
with a deterministic script, then fans that list out to the reusable
single-issue `triage-issue` skill. The actual routing / `## Build checklist` /
dependency / milestone-matching / menu / label rules **live only in
`triage-issue`** — this file does not duplicate them; see
`.claude/skills/triage-issue/SKILL.md`.

## What this skill does

1. **Gather a snapshot.** Pull the open issues (number, state, labels,
   milestone, `type:epic`/dashboard flags, comments) via the GitHub MCP
   tools — the same shape `select_triage.py` expects (see its module
   docstring for the exact schema).
2. **Run `select_triage.py`** on that snapshot to get the deterministic,
   unit-tested eligible set (open **and** labeled `ai-triage` **and not**
   `type:epic` **and not** the dashboard issue (#193) **and not** `parked`)
   plus each eligible issue's context — its current milestone and the latest
   owner `/revise`/`/redo`/`/propose` note.
3. **Invoke `triage-issue` once per eligible issue number**, passing it that
   issue's context, in **parallel with bounded concurrency** (a handful at a
   time — a model-orchestration concern for whoever runs this dispatcher, not
   something hard-coded in the script). Each issue is independent.
4. **Report** a one-line summary per issue analyzed (number → routed-to state
   + proposed milestone), and flag anything a `triage-issue` run stopped on.

## Why the split (#320)

Before this issue, `pipeline-analysis` discovered the `ai-triage` queue ad hoc
(a `list_issues` model call) and carried all of the single-issue triage logic
inline — welding "run a big round" to the actual routing rules. Splitting
discovery into a pure, unit-tested script and the single-issue flow into its
own skill (`triage-issue`) means:

- **Discovery is deterministic and testable**, matching `select_queue.py` /
  `reconcile.py` — see `select_triage.py` and its
  `tests/test_select_triage.py`.
- **Single-issue triage is a reusable unit.** `triage-issue` can be invoked
  standalone on one issue number for a quick one-off (e.g. right after an
  `/admit`, or to re-triage a single `/revise`) without a full round — see
  `triage-issue/SKILL.md` → "Invocation".
- **One source of truth.** The routing/checklist/dependency/milestone/menu/
  label rules live only in `triage-issue`, so the dispatcher and one-off runs
  can never drift apart.

This also **composes with #319**: the event-driven gatekeeper could trigger
`triage-issue` directly on the single issue that just gained `ai-triage`,
making triage near-instant per-issue too, not just a scheduled batch — a
forward-looking composition, not something this issue wires up.

## The one rule that overrides everything

**Never invent a design decision, mechanic, quest type, breed, or UI layout.**
This dispatcher never analyzes an issue itself — that rule (and its one
opt-in exception, `/propose`) lives in `triage-issue/SKILL.md`, which every
invocation goes through.

## Scope

- Act only on **open** issues labeled `ai-triage` — enforced by
  `select_triage.py`, not re-checked here.
- **Never** touch `type:epic`, the dashboard issue (#193), or any `parked`
  issue — same exclusions, enforced by `select_triage.py`.

## After dispatch

Do **not** move issues to `ready-for-work` yourself, and do not do so via
`triage-issue` either — only the gatekeeper does that on Derek's `/approve`.
