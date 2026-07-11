---
name: scaffold-core
description: >
  Scaffold a new Doggiehood.Core class and its matching NUnit test file
  following project conventions (namespaces, folder layout, Unity .meta
  files). Use when starting a new Core feature, BEFORE writing any logic —
  the scaffold leaves you at the start of the red phase.
---

# Scaffold a Core class + test

Generates the three files a new Core feature needs:

- `Assets/Scripts/Core/<Area>/<Name>.cs` — empty class, namespace
  `Doggiehood.Core.<Area>`
- `Assets/Scripts/Core/<Area>/<Name>.cs.meta` (+ folder `.meta` if the
  area is new) — fresh GUIDs so Unity imports cleanly
- `CoreTests/Doggiehood.Core.Tests/<Area>/<Name>Tests.cs` — NUnit fixture
  with one deliberately failing placeholder test

Run:

```bash
python3 .claude/skills/scaffold-core/scaffold.py <Area> <Name>
# e.g.
python3 .claude/skills/scaffold-core/scaffold.py Economy CoinWallet
```

Then follow TDD:

1. Replace the placeholder test with the first real behavior assertion.
2. `dotnet test CoreTests/Doggiehood.Core.Tests` — show red.
3. Implement the minimum in the scaffolded class — show green.

The scaffolded class body is intentionally empty: writing logic before the
failing test exists violates this repo's TDD contract.
