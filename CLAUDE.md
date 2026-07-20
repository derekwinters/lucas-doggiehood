# Doggiehood — Agent Instructions

Doggiehood is a low-poly Android game about a neighborhood of dogs, built by Derek and his son Lucas with Claude doing implementation.

## The contract

**The `/docs` site is the design and architecture contract.** Read it before implementing anything:

- `docs/specs/` — what the game is and does, organized by system. Every page has a "Build checklist" — treat those as acceptance criteria.
- `docs/engineering/` — how the project is built: tech stack, TDD testing strategy, versioning, CI/CD, and this workflow itself.
- `docs/intro/` — vision and conventions, useful context but not itself full of build rules.

Build the docs site locally with `pip install -r docs/requirements.txt && mkdocs serve` if you want to browse it rendered.

**GitHub issues are the checklist for accomplishing the contract.** Each issue carries a detailed TDD-oriented checklist. Work one issue at a time within the current **focus milestone**, lowest issue number first. Milestones are version-numbered planning scopes (`v0.4`, `v1.0`, …), matched live by their description — see `docs/intro/conventions.md`.

If an issue and the docs ever disagree, or something needed isn't covered by either, stop and flag it — don't invent a rule. Decisions get made in conversation with Derek and Lucas, captured as GitHub issues, then reflected in `/docs`.

## Tasks that need Derek or Lucas

Anything an agent cannot complete autonomously — repo settings, secrets, asset creation, unanswered design questions, on-device playtesting — goes in the **`Direct Involvement Needed`** milestone as **one small issue per task**, assigned to Derek, stating the single action needed and how to verify it's done. Never collect human follow-ups into one big multi-section checklist issue.

## Non-negotiable engineering rules

1. **Strict TDD.** Write a failing test, run it, show the failure, implement the minimum to pass, run it, show the pass, refactor if needed. Never write implementation before its failing test exists. See `docs/engineering/testing.md`.
2. **Core/Unity split.** New game logic defaults to a Unity-independent Core C# assembly (plain NUnit, no engine dependency). Only the thin `MonoBehaviour`/scene-wiring layer touches UnityEngine. See `docs/engineering/tech-stack.md`.
3. **Conventional Commits — every commit, every PR title, no exceptions.** Every commit message *and* every pull request title follows [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, `docs:`, etc.), going forward from this point on. This isn't optional or best-effort — release-please's versioning depends on it, and a non-conforming commit or PR title should be corrected (amended/retitled) before merging, not left as-is. See `docs/engineering/versioning.md`.
4. **Version lives in `/VERSION`.** Don't hand-edit Unity's `ProjectSettings.asset` version fields; release-please owns `/VERSION`.
5. **Every agent PR starts with a `## Deviations and Decisions` section** — present even with no findings. Deviations: anything not fully compliant with the prompt/specs/docs, each explained in a sentence or two so the reader can confirm it's still acceptable. Decisions: judgment calls made mid-run because the prompt/specs/docs were less clear than expected, each with the choice explained and a note on how to prevent the gap next time. See `docs/engineering/agent-workflow.md`.
6. **Never guess Unity serialization.** Before hand-authoring `.meta` files or `ProjectSettings.asset` blocks, read `docs/engineering/unity-serialization.md` — verify key names and enum values against real project files (Unity silently ignores unknown entries at build time), pin GUIDs (and Sprite internal IDs) for anything referenced by GUID, and guard the wiring with serialization-level EditMode tests.
7. **Rebase feature branches; don't merge the base back in.** When your working branch's base (e.g. `main`) has moved, bring the branch back into alignment by rebasing it onto the updated base (`git fetch origin main && git rebase origin/main`), not by merging the base into the branch. Keep feature-branch history linear — no `Merge branch 'main' into …` commits.
8. **Wireframe before UI code.** Every UI screen, panel, or overlay requires an approved wireframe (a structured text spec plus a matching HTML mockup) before any implementation code is written — including graybox. If an issue touches a screen's structure and no approved wireframe exists in `docs/specs/ui/`, stop and flag it rather than inventing a layout. See `docs/engineering/ui-design-process.md`.
9. **Every PR reconciles the docs/specs it affects.** If a change adds, removes, or alters behavior, layout, or a design decision, update the relevant `docs/specs` (or `docs/engineering`) page(s) in the **same** PR. If no doc change is needed, say so explicitly in the mandatory `## Deviations and Decisions` section, with the reason. CI enforces a conscious decision: the `docs-test` `build` gate runs on every PR and fails a code-only PR unless it carries the `skip-docs` label. See `docs/engineering/agent-workflow.md`.

## What not to do

- Don't add monetization, ads, accounts, or network calls — the product is free, offline, local-save-only (`docs/specs/product-scope.md`).
- Don't invent quest types, personalities, breeds, or mechanics not in the specs — bring new ideas back to Derek and Lucas first.
