# How to Play

This describes the player-facing experience as currently designed. It's written from the player's point of view — for the underlying rules a developer implements against, see the linked spec pages.

## Exploring

You view the neighborhood from an angled, isometric top-down camera — like looking down at a diorama. Drag to pan around, pinch to zoom in and out. There's no on-screen avatar; you interact directly with the world by tapping on it. The app runs in landscape. *(Spec: [Camera, Navigation & Controls](../specs/world/camera-controls.md))*

## Meeting the dogs

Dogs live at houses around the neighborhood and wander the streets nearby. Some are inside, looking out a window. Each dog has a breed, a name, and a personality — a Brave dog acts differently than a Shy one, and (eventually) even walks differently. *(Spec: [Dog Behavior](../specs/dogs/behavior.md), [Dog Roster & Names](../specs/dogs/roster-names.md))*

A dog with something to say shows a speech bubble above it. Tap the bubble to start a short conversation — the dog explains what it needs, you tap to help, done. No branching conversation trees, no quest log to check — the speech bubble is the only signal you need. *(Spec: [Conversation System](../specs/quests/conversation-system.md))*

## Helping out

A dog's request is one of a few kinds:

- **Lost something** — a toy, or its own puppy — hidden somewhere in the visible neighborhood. Pan and zoom around until you spot it, then tap it.
- **Wants something bought** — a toy, a pool, a comfort item for its yard. Accept, and a delivery truck drives up and drops the package at the dog's door. The dog itself heads home and sits waiting for it.
- **Has a bug problem** — spray to clear it out.

Every completed request pays out a small amount of coin. *(Spec: [Quest & Economy](../specs/quests/economy.md), [Quest Content](../specs/quests/quest-content.md))*

## Spending coin

Coin goes toward gifts for dogs and comfort decorations for their yards — a bed, a cushion, a blanket that the dog will wander over and use on its own from time to time. Decorations make a dog visibly happier, though happiness is flavor, not a mechanic you need to manage. Whatever you deliver stays in the world for good. *(Spec: [Decorations](../specs/decorations.md))*

## Starting small

The very first neighborhood is deliberately tiny: one intersection, two streets, four houses. Expanding to new streets and zones is real, planned, and fully designed — just not part of the first playable version of the game. *(Spec: [Neighborhood Expansion](../specs/expansion.md) — post-MVP)*
