# Design & Architecture Specs

This section is the implementation contract. Each page is a settled design area; every rule on it has already been decided (traceable to a GitHub issue) and is either fully specified or explicitly flagged as a placeholder pending real content.

A development agent implementing a feature should treat these pages as authoritative. If something needed to implement a feature isn't covered here, that's a gap — stop and flag it rather than inventing a rule, and it should get resolved back in GitHub first.

## v1.0 scope

v1.0 — the first complete release — integrates all mainline functionality into one playable build across the 4 starting houses, with real art, polish, and onboarding. The bulk of that functionality lands first in graybox under **`v0.4`**, which now also includes **neighborhood expansion** ([#88](https://github.com/derekwinters/lucas-doggiehood/issues/88)) — the [Neighborhood Expansion](expansion.md) page is fully designed and no longer deferred. See [Conventions](../intro/conventions.md#milestones-are-version-numbered-scopes) for the version-milestone model.

## Pages

**World**

- [World & Neighborhood](world/world.md) — the physical setting
- [Camera, Navigation & Controls](world/camera-controls.md) — how the player sees and moves
- [Art & UI Style](world/art-style.md) — palette, architecture, and interface chrome
- [Sidewalks & Walk Network](world/sidewalks.md) — sidewalk placement, crosswalks, and how dogs pathfind

**Dogs**

- [Dog Behavior](dogs/behavior.md) — how dogs move, live, and act
- [Dog Roster & Names](dogs/roster-names.md) — the starting cast and the naming system

**Conversation & Quests**

- [Conversation System](quests/conversation-system.md) — talking to a dog
- [Quest & Economy](quests/economy.md) — the core play loop and currency
- [Quest Content](quests/quest-content.md) — the actual quest types

**UI Wireframes**

- [UI Wireframes Overview](ui/index.md) — the layout contract for every screen, panel, and overlay (see the [UI Design Process](../engineering/ui-design-process.md))
- [Shared UI Components](ui/shared-components.md) — reusable atomic pieces referenced by every screen

**Other systems**

- [Decorations](decorations.md)
- [Neighborhood Expansion](expansion.md) *(v0.4)*
- [Audio](audio.md)
- [Onboarding](onboarding.md)
- [Product Scope & Constraints](product-scope.md)
- [Future Ideas](future-ideas.md) *(explicitly out of scope, kept for later)*
