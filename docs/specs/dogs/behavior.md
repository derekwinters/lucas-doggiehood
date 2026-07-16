# Dog Behavior

*Epic: [#3](https://github.com/derekwinters/lucas-doggiehood/issues/3)*

## Movement

Dogs wander/patrol the streets and are visibly roaming the neighborhood; the player can approach and interact with them. ([#8](https://github.com/derekwinters/lucas-doggiehood/issues/8))

Some dogs are placed inside houses rather than out on the street, visible looking out through a window, rather than roaming. ([#9](https://github.com/derekwinters/lucas-doggiehood/issues/9))

### Movement conveys personality

Rather than giving every personality its own animation set, mood is conveyed through **how a dog walks** ([#89](https://github.com/derekwinters/lucas-doggiehood/issues/89)):

- **Excited**: fast walking speed, long straight stretches down a street before turning — covers real distance rather than pacing back and forth.
- **Grumpy/sad**: slow walking speed, turns almost every step — shuffles around a small area. *(Deferred out of MVP — see below.)*

Other personalities (Brave, Shy, Adventurous/Exploring, Athletic) can get their own speed/turn-pattern combinations later using the same system.

**MVP scope**: only the general speed/turn-pattern system needs to exist. **Excited** is the one pattern to actually implement for MVP. Grumpy's distinct movement pattern is deferred — Grumpy remains a valid personality for dialogue/flavor (Pepper is Grumpy, see [Dog Roster & Names](roster-names.md)), it just won't have distinct movement behavior yet.

### Quest-related movement

When a dog's "buy me X" request is accepted, the dog walks home at a medium speed — heading straight back if it isn't already there — then sits and waits (see [animation](#animations)) until the delivery truck arrives. See [Quest Content](../quests/quest-content.md).

## Population

Across the 4 starting houses: most houses have a parent dog + puppy, some houses have just one dog, and some have 2-3 dogs. This variety sets up quests like "lost my puppy" naturally. ([#34](https://github.com/derekwinters/lucas-doggiehood/issues/34)) See [Dog Roster & Names](roster-names.md) for the actual starting cast.

New houses (post-MVP expansion) start empty and dogs move in gradually over time — see [Neighborhood Expansion](../expansion.md).

## Breeds & personality

Breed is a **data** attribute (the `Breed` enum), not a distinct mesh. Every dog renders with the same **standard shared model** — the Kenney "Cube Pets" model (`Assets/Art/Dogs/CubePets/Resources/animal-dog.fbx`, wired in `DogView`, [#123](https://github.com/derekwinters/lucas-doggiehood/issues/123)) — and breeds are distinguished visually by **coat color/tint** (see [Art & UI Style](../world/art-style.md)), not by a per-breed mesh. Breed still gives each dog a clear identity through its name, coat, and personality. ([#35](https://github.com/derekwinters/lucas-doggiehood/issues/35), [#166](https://github.com/derekwinters/lucas-doggiehood/issues/166)) *(Decision 2026-07-16, Derek: Cube Pets is the single standard dog model, superseding the earlier "distinct model per breed" direction.)*

Every dog has a defined personality trait that colors its dialogue tone and the kinds of quests it gives. ([#36](https://github.com/derekwinters/lucas-doggiehood/issues/36))

**Personality types**: Brave, Adventurous/Exploring, Shy, Excited, Grumpy, Athletic.

## Scope

Dogs are the only interactable characters for MVP — no cats, mail carriers, squirrels, or other animals/people. Scope is limited to dogs and their houses; other characters can be revisited in future expansions. ([#37](https://github.com/derekwinters/lucas-doggiehood/issues/37))

## Animations

Full animation/pose list — see [#66](https://github.com/derekwinters/lucas-doggiehood/issues/66) for the source issue:

| Pose | When it's used |
|---|---|
| Idle/wander | Base walking state; speed/turn-pattern varies by personality (see above) |
| Rest | Lying down — used when a dog uses a comfort decoration ([Decorations](../decorations.md)) |
| Sit | Waiting — used when a dog has walked home after accepting a quest and is waiting for the delivery truck |
| Window-watching | Dogs placed inside houses, looking out a window |

No separate animation is needed for: conversation start (the speech bubble appearing is the only signal — see [Conversation System](../quests/conversation-system.md)), happiness reactions beyond rest/idle, or the delivery truck's arrival (the truck is the actor, not the dog).

## Build checklist

- [ ] Dogs wander streets at a base walking speed and can be tapped/interacted with
- [ ] Some dogs are placed inside houses in a static window-watching pose instead of wandering
- [ ] General speed/turn-pattern movement system exists, with the **Excited** pattern implemented (fast + long straight stretches)
- [ ] Grumpy personality exists for dialogue/flavor purposes but has no distinct movement pattern yet
- [ ] Each dog has a breed (a `Breed`-enum data value, distinguished visually by coat/tint on the shared standard Cube Pets model — not a distinct per-breed mesh) and a personality trait
- [ ] Only dogs are interactable in the world — no other animal/person NPCs
- [ ] Rest, sit, idle/wander, and window-watching poses are all implemented
- [ ] Dog walks home and sits after accepting a "buy me X" quest, until the delivery truck arrives
