# Project Conventions

## GitHub is the source of truth for decisions; this site is the summary

New ideas, open questions, and brainstorming happen as conversation and get captured as GitHub issues right away. This documentation site is the clean, current distillation of whatever's been *decided* — it's rewritten as decisions change, while the GitHub issues stay as the historical record of how each decision was reached.

## Labels

- **`area:*`** — what part of the game an issue touches: `gameplay`, `art`, `audio`, `ui`, `story`, `build`, `ai`.
- **`type:*`** — `epic` (a tracking issue with sub-issues), `task`, `question`, `bug`.
- No label for who owns a decision — Derek and Lucas both weigh in on everything; `area:` is enough to know what kind of decision it is.

### Pipeline labels

These labels are the state machine for the [AI issue-management pipeline](../engineering/issue-pipeline.md) — a routine moves issues between them in response to Derek's issue-comment commands:

- **`ai-triage`** — admitted for AI analysis.
- **`pending-approval`** — analysis done; awaiting Derek's `/approve`.
- **`needs-clarification`** — analysis posted a question; awaiting an answer.
- **`ready-for-work`** — approved and milestoned; in the nightly dev queue.
- **`in-progress`** — a nightly dev run has picked it up.
- **`parked`** — hidden from every routine and the dashboard.
- **`dashboard`** — marks the single live dashboard issue; excluded everywhere.

## Milestones define build order, not just grouping

Milestones are numbered so their build order is unambiguous regardless of how GitHub sorts them:

| Milestone | Scope |
|---|---|
| `00 - Concepts & Core Mechanics` | Standing design/product decisions with no single build phase (monetization, offline-only, target audience) |
| `01 - Project Setup` | Repo, CI/CD, docs site, TDD architecture, the development agent |
| `02 - World & Camera Foundation` | Static scene: camera, navigation, starting neighborhood art — no dogs yet |
| `03 - Dogs & Conversations` | Dogs wander and talk — no quest logic wired in yet |
| `04 - Quests & Economy` | The quest system, currency, and the 3 initial quest types working end-to-end |
| `05 - Decorations & Happiness` | Yard decorations and the happiness system |
| `06 - Neighborhood Expansion` | Zone unlocking, house building — **post-MVP** ([#88](https://github.com/derekwinters/lucas-doggiehood/issues/88)) |
| `07 - Polish & Onboarding` | Audio, first-launch tutorial, art/UI/animation polish |
| `08 - Vertical Slice Release Candidate` | Everything integrated and playable — this is the MVP |

## Epics and sub-issues

A design area gets an **epic** issue (e.g. "Epic: Dog NPC Behavior") whose body is a short overview. Individual decisions are filed as **sub-issues** of that epic, one decision per issue, so the epic's checklist shows progress at a glance.

## When a question closes vs. stays open

- If the answer to an `OPEN QUESTION` issue is itself content that still needs to be *built* (a spec, a list, a roster, a number) — it gets updated with the decision and **stays open** until that content actually exists in the project.
- It only **closes** when the real, ongoing tracking work has moved entirely into other already-open issues (for example, a resolved scope question that spawned a whole new epic).

## Docs versioning

This site is built with [MkDocs](https://www.mkdocs.org/) + [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/), deployed with [mike](https://github.com/jimporter/mike) so multiple versions of the docs stay browsable side by side, tagged to match the game's own version (from the root `VERSION` file / release-please tags — see [Versioning & Releases](../engineering/versioning.md)). See [CI/CD](../engineering/ci-cd.md) for how the site builds and publishes.
