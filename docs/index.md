# Doggiehood

Doggiehood is a cozy, low-poly Android game about a neighborhood of dogs — built by [Derek Winters](https://github.com/derekwinters) and his son Lucas, with Claude doing the implementation work.

You wander the neighborhood from above, spot dogs with something to say, and help them out: find a lost toy, deliver a birthday gift, spray away a bug problem. There's no fail state, no monetization, and no rush — just a small, growing, good-natured place to visit.

## What this documentation is

This site is the **contract** for what Doggiehood is and how it gets built. It's split into two audiences:

- **[Introduction](intro/vision.md)** — the pitch, the feel of the game, and how the project runs. Start here if you're new.
- **[Design & Architecture Specs](specs/index.md)** — the detailed, decided rules a development agent implements against. If code and a spec ever disagree, the spec wins until someone deliberately changes it (in GitHub, then here).

There's also an **[Engineering](engineering/tech-stack.md)** section covering the technical side: Unity setup, the TDD workflow, and how releases are versioned.

## Where decisions live

Every rule in this site traces back to a GitHub issue in [derekwinters/lucas-doggiehood](https://github.com/derekwinters/lucas-doggiehood), organized into epics and version-numbered milestones (`v0.4`, `v1.0`, …) that carry each version's scope. The issues are the day-to-day scratchpad where new ideas get discussed and settled; this site is the clean, current summary of what's been decided. When you see `#NN` in these docs, that's the GitHub issue where the decision was made.
