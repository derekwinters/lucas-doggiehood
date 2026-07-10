# Doggiehood — Agent Instructions

Doggiehood is a low-poly Android game about a neighborhood of dogs, built by Derek and his son Lucas with Claude doing implementation.

## The contract

**The `/docs` site is the design and architecture contract.** Read it before implementing anything:

- `docs/specs/` — what the game is and does, organized by system. Every page has a "Build checklist" — treat those as acceptance criteria.
- `docs/engineering/` — how the project is built: tech stack, TDD testing strategy, versioning, CI/CD, and this workflow itself.
- `docs/intro/` — vision and conventions, useful context but not itself full of build rules.

Build the docs site locally with `pip install -r docs/requirements.txt && mkdocs serve` if you want to browse it rendered.

**GitHub issues are the checklist for accomplishing the contract.** Each issue needed for MVP has a detailed TDD-oriented checklist. Work one issue at a time, in milestone order (`02` → `08`, lowest issue number first within a milestone — see `docs/intro/conventions.md`).

If an issue and the docs ever disagree, or something needed isn't covered by either, stop and flag it — don't invent a rule. Decisions get made in conversation with Derek and Lucas, captured as GitHub issues, then reflected in `/docs`.

## Non-negotiable engineering rules

1. **Strict TDD.** Write a failing test, run it, show the failure, implement the minimum to pass, run it, show the pass, refactor if needed. Never write implementation before its failing test exists. See `docs/engineering/testing.md`.
2. **Core/Unity split.** New game logic defaults to a Unity-independent Core C# assembly (plain NUnit, no engine dependency). Only the thin `MonoBehaviour`/scene-wiring layer touches UnityEngine. See `docs/engineering/tech-stack.md`.
3. **Conventional Commits — every commit, every PR title, no exceptions.** Every commit message *and* every pull request title follows [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, `docs:`, etc.), going forward from this point on. This isn't optional or best-effort — release-please's versioning depends on it, and a non-conforming commit or PR title should be corrected (amended/retitled) before merging, not left as-is. See `docs/engineering/versioning.md`.
4. **Version lives in `/VERSION`.** Don't hand-edit Unity's `ProjectSettings.asset` version fields; release-please owns `/VERSION`.

## What not to do

- Don't add monetization, ads, accounts, or network calls — the product is free, offline, local-save-only (`docs/specs/product-scope.md`).
- Don't build `06 - Neighborhood Expansion` content as part of the MVP milestone — it's fully specced but explicitly post-MVP (`docs/specs/expansion.md`).
- Don't invent quest types, personalities, breeds, or mechanics not in the specs — bring new ideas back to Derek and Lucas first.
