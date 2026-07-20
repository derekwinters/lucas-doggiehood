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
- **`dashboard`** — marks the single live dashboard issue; excluded from every routine, except that `/focus` is honored on it so focus can be set from the dashboard itself ([#204](https://github.com/derekwinters/lucas-doggiehood/issues/204)).

## Milestones are version-numbered scopes

Each milestone's **title is a version** (`v0.4`, `v1.0`, `v1.1`, `v2.0`, …) and its **description carries the scope** — what that version adds, expands, or polishes. Pick a milestone by matching an issue against the milestone's scope *description*, read live, rather than a memorized phase list.

These version numbers are **planning labels, not release numbers.** The shipped app version lives in [`/VERSION`](../engineering/versioning.md) and is computed by release-please from Conventional Commits; a milestone titled `v0.4` is a *target scope*, and the actual version that ships when its work lands may differ. The two are deliberately decoupled.

Milestones are **forward-looking planning buckets.** A version that has already shipped lives as a git tag / GitHub release, not a milestone — so there is no `v0.3` milestone once v0.3 is out; any remaining open work simply moves to the next planning milestone.

The current planning milestones:

| Milestone | Scope |
|---|---|
| `v0.4` | All mainline functionality, in graybox — decorations, happiness, neighborhood expansion, remaining core systems. Get the whole game *working*; art stays rough. |
| `v1.0` | First complete release: the last group of changes required to call it v1 — real art, polish, onboarding, final integration. |
| `v1.1` | Improvements, changes, and playtest fixes made after v1. |
| `v2.0` | Major redesigns and large expansions. |

Between the named milestones we spin up further version milestones (`v0.5`, `v0.6`, …) as we need them — each with its own focus (polish, playtest fixes, a larger map expansion) — rather than following a fixed pre-planned ladder.

Two **non-version** milestones sit alongside these:

- **`Direct Involvement Needed`** — tasks no agent can complete (asset creation, on-device checks, unanswered questions). Not a version; excluded from version ordering.
- The early setup and shipped phase milestones (`00`–`08`) remain **closed** as historical record; they are not renamed to versions.

Nightly development targets one **focus milestone** at a time (set with `/focus <version>`; see the [issue pipeline](../engineering/issue-pipeline.md)).

## Every issue names the spec pages it touches

An issue that changes player-facing behavior, layout, or a design decision should name — in its body or checklist — which [`docs/specs`](../specs/index.md) (or `docs/engineering`) page(s) it expects to update, or note that none apply. This makes the spec reconciliation a planned deliverable rather than an afterthought: the matching page is updated in the **same** PR as the code (a [non-negotiable rule](../engineering/agent-workflow.md#how-an-issue-gets-worked)), and CI's always-on `docs-test` gate forces a conscious decision — a code-only PR fails unless it carries the `skip-docs` label.

## Epics and sub-issues

A design area gets an **epic** issue (e.g. "Epic: Dog NPC Behavior") whose body is a short overview. Individual decisions are filed as **sub-issues** of that epic, one decision per issue, so the epic's checklist shows progress at a glance.

## When a question closes vs. stays open

- If the answer to an `OPEN QUESTION` issue is itself content that still needs to be *built* (a spec, a list, a roster, a number) — it gets updated with the decision and **stays open** until that content actually exists in the project.
- It only **closes** when the real, ongoing tracking work has moved entirely into other already-open issues (for example, a resolved scope question that spawned a whole new epic).

## Docs versioning

This site is built with [MkDocs](https://www.mkdocs.org/) + [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/), deployed with [mike](https://github.com/jimporter/mike) so multiple versions of the docs stay browsable side by side, tagged to match the game's own version (from the root `VERSION` file / release-please tags — see [Versioning & Releases](../engineering/versioning.md)). See [CI/CD](../engineering/ci-cd.md) for how the site builds and publishes.
