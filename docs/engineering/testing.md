# Testing Strategy: TDD, No Test Environment Required

*Issues: [#72](https://github.com/derekwinters/lucas-doggiehood/issues/72) (architecture), [#77](https://github.com/derekwinters/lucas-doggiehood/issues/77) (agent enforcement)*

The goal is thorough test coverage **without needing an emulator, device, or even the Unity Editor** for most of it. This is possible because of the [Core/Unity split](tech-stack.md#code-architecture-core-unity-split): almost all real game logic lives in plain C# with no engine dependency.

## Red-green-refactor, strictly enforced

The development agent enforces strict TDD on every change:

1. Write a failing test first.
2. Run it and show the failure (red).
3. Implement the minimum code to make it pass.
4. Run it and show the pass (green).
5. Refactor if needed, re-run to confirm still green.

No implementation code is written before its failing test exists and has been shown to fail. This isn't a style preference — it's a hard requirement for how the agent works in this repo.

## Where tests live

- **Core logic** (quests, economy, dog state, name pool, house leveling, etc.): plain NUnit tests, no Unity dependency. These run anywhere, instantly — the primary source of coverage.
- **Unity integration layer** (`MonoBehaviour` wiring, scene setup): Unity Test Framework EditMode tests, run headless in CI via `-batchmode -nographics`. No PlayMode tests and no on-device/emulator testing are required for v1.0 — the thin Unity layer should have as little untested logic in it as possible by design (push everything possible into Core).

## What this means for new features

When implementing anything from the [specs](../specs/index.md), default to:

1. Model the behavior as plain C# in a Core assembly.
2. Write NUnit tests against that Core class describing the spec's build-checklist items directly.
3. Only once the logic is proven, wire a thin Unity-side adapter to it, with an EditMode test if the wiring itself has meaningful logic (event subscriptions, etc.) — not just a pass-through.

If a feature seems to *require* Unity APIs to even express the logic (e.g. actual physics-based movement), isolate the engine-dependent part as narrowly as possible and keep decision logic (what to do) separate from execution (how Unity does it).

## Known limitation: EditMode tests run in CI, not in agent environments

Agent execution environments do not have the Unity Editor installed, so EditMode tests cannot be run locally during an agent session. The sanctioned flow is:

1. Write the EditMode test first, before the implementation exists — the red phase is the test referencing a type or member that doesn't exist yet (a compile error).
2. Implement the minimum to satisfy it.
3. CI runs the EditMode suite headlessly (`-batchmode -nographics`) on the PR and is the authoritative green.

This is the expected, documented workflow — **not** a deviation. Agent PRs should not list "EditMode tests were not executed locally" in their `## Deviations and Decisions` section; it's only reportable if CI itself fails or the test-first ordering wasn't followed.
