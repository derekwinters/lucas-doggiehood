---
name: core-unity-split
description: >
  Quick reference for Doggiehood's Core-vs-Unity assembly split — which
  assembly new code belongs in, the folder/asmdef layout, and the rules
  that keep Core engine-free. Use when deciding where a new class goes or
  when setting up references between assemblies.
---

# Core vs. Unity layer — where does code go?

Full contract: docs/engineering/tech-stack.md and testing.md. Decision
rule: **if it decides anything, it's Core; if it touches the engine, it's
the thinnest possible Unity adapter.**

## Layout

| Assembly | Location | May reference | Tested by |
|---|---|---|---|
| `Doggiehood.Core` | `Assets/Scripts/Core/` | nothing engine-side (`noEngineReferences: true`) | plain NUnit: `dotnet test CoreTests/Doggiehood.Core.Tests` |
| `Doggiehood.Unity` | `Assets/Scripts/Unity/` | `Doggiehood.Core`, UnityEngine | EditMode tests (below), only when wiring has logic |
| `Doggiehood.Unity.EditModeTests` | `Assets/Tests/EditMode/` | both + TestRunner | runs headless in CI (`ci-tests.yml`) |
| Core test harness (not a Unity assembly) | `CoreTests/Doggiehood.Core.Tests/` | compiles Core sources directly via glob | — |

## Goes in Core (plain C#)

Quest logic and templates, economy/currency math, dog state and
personality data, name-pool selection, happiness rules, decoration
inventory, save-data model, camera *bounds math*, movement *decisions*
(where to go next) — anything expressible as data-in/data-out.

## Goes in the Unity layer (thin!)

MonoBehaviours that forward to Core, scene loading, input capture,
animation/audio triggering, rendering, physics queries, actual
`transform` mutation. Pattern: Unity captures input/state → asks a Core
type what should happen → executes the answer with Unity APIs.

## Rules that keep this honest

- Core's asmdef keeps `noEngineReferences: true` — never flip it. If a
  Core class "needs" a Unity type, define a plain data type (e.g. an
  `(x, y)` struct) in Core and convert at the Unity boundary.
- New logic defaults to Core; putting it in the Unity layer needs a
  reason (a genuine engine dependency), not the other way around.
- Every file under `Assets/` needs a `.meta` (use the scaffold-core skill
  for new Core classes — it handles metas and conventions).
- C# language level: what Unity supports (LangVersion 9.0 in the test
  csproj enforces this for Core).
