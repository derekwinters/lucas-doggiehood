# Development Agent & Workflow

*Issues: [#77](https://github.com/derekwinters/lucas-doggiehood/issues/77), [#78](https://github.com/derekwinters/lucas-doggiehood/issues/78), [#84](https://github.com/derekwinters/lucas-doggiehood/issues/84), [#135](https://github.com/derekwinters/lucas-doggiehood/issues/135)*

## Who writes the code

A dedicated Claude Code agent (`.claude/agents/`), not ad-hoc chat sessions, does the actual Unity/C# implementation work. It:

- Enforces strict [TDD](testing.md) — red, green, refactor, every time, no exceptions.
- Defaults new logic to the Unity-independent Core assembly (see [Tech Stack](tech-stack.md#code-architecture-core-unity-split)).
- Follows Conventional Commits for everything it commits, since [release-please](versioning.md) depends on them.
- Works from one GitHub issue at a time, using that issue's build checklist as its acceptance criteria.

## Supporting skills

Skills back the agent's day-to-day workflow:

- Running Core (plain NUnit) and Unity EditMode tests headlessly and reporting results.
- Scaffolding a new Core class + matching test file following project conventions.
- A reference for Unity project/assembly-definition conventions — what belongs in Core vs. the Unity layer.

The exact skill list is refined as the agent and initial project structure come online.

## CLAUDE.md

A `CLAUDE.md` at the repo root captures durable conventions so the agent doesn't need to re-derive them each session: the Core/Unity split, the TDD workflow, the Conventional Commit requirement, `VERSION` file usage, and — importantly — where design decisions actually live (this docs site, sourced from GitHub issues).

## How an issue gets worked

1. Pick the next open issue in build-order (milestones `02` → `08`, lowest number first within a milestone).
2. Read its build checklist and the doc page(s) it links to for full context — the issue is the checklist, the docs are the contract.
3. Work test-first per [Testing Strategy](testing.md), checking off checklist items as they're satisfied.
4. **Reflect**: review the run for deviations and mid-run decisions (see below).
5. Commit with a Conventional Commit message; open a PR whose body starts with the `Deviations and Decisions` section.
6. If something in the issue conflicts with the docs, or a decision is missing entirely, stop and flag it rather than guessing — that's a design gap to resolve back in GitHub, then reflected here.

## PR reflection: Deviations and Decisions

Every agent PR body begins with a `## Deviations and Decisions` header — present even when there are no findings.

- **Deviations** are changes the agent made that were not fully compliant with the prompt, specs, or docs. Undesired in general, but a possible outcome of a valid test/development flow. The agent identifies each one and explains why in a sentence or two, so the reviewer can confirm the deviation is still acceptable. Known limitations already documented in these docs (e.g. [EditMode tests run in CI, not locally](testing.md#known-limitation-editmode-tests-run-in-ci-not-in-agent-environments)) are the sanctioned workflow and are **not** deviations — don't re-report them.
- **Decisions** are judgment calls the agent had to make mid-run. Major logic and implementation decisions should already live in documentation, specs, or the prompt — the agent should be able to run without weighing options in nearly all scenarios. When a decision was unavoidable, the agent documents what information or instructions were not as clear as expected, explains the choice in a sentence or two, and identifies how the gap could be prevented in the future.
