# Development Agent & Workflow

*Issues: [#77](https://github.com/derekwinters/lucas-doggiehood/issues/77), [#78](https://github.com/derekwinters/lucas-doggiehood/issues/78), [#84](https://github.com/derekwinters/lucas-doggiehood/issues/84)*

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
4. Commit with a Conventional Commit message; open a PR.
5. If something in the issue conflicts with the docs, or a decision is missing entirely, stop and flag it rather than guessing — that's a design gap to resolve back in GitHub, then reflected here.
