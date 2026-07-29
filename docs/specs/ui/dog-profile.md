# Dog profile

*Wireframe issue: [#177](https://github.com/derekwinters/lucas-doggiehood/issues/177). Implements/covers: dog profile view (opened per [#165](https://github.com/derekwinters/lucas-doggiehood/issues/165)). Approved: Derek, 2026-07-24 (in-session).*
*Mockup: [mockups/dog-profile.html](mockups/dog-profile.html).*

## Purpose

Tapping a dog opens its profile ([#165](https://github.com/derekwinters/lucas-doggiehood/issues/165)) — a centered card over a dimmed scene showing who the dog is. This is the **dog-only** profile; the mirrored house profile is out of scope here and tracked in [#208](https://github.com/derekwinters/lucas-doggiehood/issues/208). Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Header | Dog portrait · name · breed chip | [Shared panel chrome](shared-components.md) |
| Stats | Age tile · Personality tile | [Shared panel chrome](shared-components.md) |
| Footer action | The **Home** button (pans the camera to the dog's house) | [PillButton](shared-components.md#pill-button-pillbutton) |
| Close | Top-right ✕ dismiss affordance | [Shared panel chrome](shared-components.md) |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `ProfileAnchor` | `Center` | Card position — centered over a dim scrim so the neighborhood stays visible behind it |
| `ProfileWidthPx` | `900` | Card width |
| `ProfilePaddingPx` | `48` | Card inset (padding) |
| `PortraitSizePx` | `220` | Dog portrait |
| `CloseButtonSizePx` | `72` | Close (✕) button, top-right |

The panel **chrome** (outline 6 / corner radius 40 / drop-shadow 12–14) and the **Home** button (96 px [PillButton](shared-components.md#pill-button-pillbutton)) are owned by the shared components ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) — neither is re-specified here; this page only places those components and sizes the card, portrait, and close button.

## Notes

- **Home button behavior.** The **Home** button closes the profile and pans/zooms the camera to that dog's house, with a brief highlight on arrival. There is **no address text** — the world has no street names, so "home" is a place the camera flies to, not an address.
- **Data.** Name, breed, age, and personality read from the dog's Core data (the authored roster — see [Dog Roster & Names](../dogs/roster-names.md)); this page fixes layout only. The field values shown in the mockup are placeholders.
- **Age = life stage ([#165](https://github.com/derekwinters/lucas-doggiehood/issues/165)).** No numeric age is authored anywhere in the roster; the only age-related Core datum is the puppy flag. The Age tile therefore reads the dog's life stage — **Puppy** or **Adult** — rather than a number of years (the mockup's "3 years" is a placeholder). If per-dog numeric ages are ever wanted, they need authoring in the roster first.
- **Opening the profile ([#165](https://github.com/derekwinters/lucas-doggiehood/issues/165)).** Tapping a dog's **body** opens this profile. The dog's **speech bubble** stays the conversation surface ([Conversation System](../quests/conversation-system.md), [#11](https://github.com/derekwinters/lucas-doggiehood/issues/11)) — the two affordances are split so the bubble remains the sole quest-discovery mechanism.
- **Scope.** Dog profile only — the reciprocal house profile (level, owners, dog links) is out of scope and tracked in [#208](https://github.com/derekwinters/lucas-doggiehood/issues/208).
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
