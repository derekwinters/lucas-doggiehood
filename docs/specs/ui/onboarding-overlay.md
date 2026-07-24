# Onboarding overlay

*Wireframe issue: [#176](https://github.com/derekwinters/lucas-doggiehood/issues/176). Implements/covers: `OnboardingOverlay`. Approved: Derek, 2026-07-24 (in-session).*
*Mockup: [mockups/onboarding-overlay.html](mockups/onboarding-overlay.html).*

## Purpose

First-launch guidance ([#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)/[#44](https://github.com/derekwinters/lucas-doggiehood/issues/44)), layered over live gameplay — there is **no blocking modal**. A slim coach prompt sits **bottom-center** and advances itself through the four onboarding steps (pan, zoom, tap the speech bubble, complete the first quest) as the player performs each real action, then auto-dismisses for good. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

The whole overlay is a single **coach bar** — a slim pill floated over the neighborhood. It carries three inline regions:

| Region | Contains | Shared component |
|---|---|---|
| Leading badge | A round paw badge that marks the prompt as the coach's voice | Reuses the Candy Cottage baseline (thick outline, hard shadow, pill) from [Shared UI Components](shared-components.md) / [Art & UI Style](../world/art-style.md) |
| Message text | The current step's guidance text (from the Core `OnboardingSequence`) | — |
| Trailing step-dots | A row of `StepDotCount` progress dots; the current step's dot is filled | — |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `CoachAnchor` | `BottomCenter` | Coach bar position — sits bottom-center so the dog and neighborhood stay visible above it |
| `CoachWidthPx` | `900` | Coach bar width (centered) |
| `CoachHeightPx` | `88` | Coach bar height |
| `CoachBottomMarginPx` | `56` | Gap below the coach bar |
| `StepDotCount` | `4` | Number of progress dots (one per guided step) |
| `MsgFontPx` | `30` | Step message text size |

The bar's **style** — thick outline, hard drop shadow, pill (999 px) radius — is the shared Candy Cottage baseline from [Shared UI Components](shared-components.md) / [Art & UI Style](../world/art-style.md) and is not re-specified here; this page places the bar and sizes it and its message text.

## Notes

- **Runs once, over live gameplay.** The sequence runs only on first launch (driven by the Core `OnboardingSequence`) and never blocks input — it floats over the real neighborhood, real dog, real quest, and real reward, not a scripted tutorial scene.
- **Advances on the real action.** Each step advances automatically when the player performs the actual action — pan, then zoom, then tap the speech bubble, then complete the quest — and the overlay auto-dismisses after step 4.
- **Panel-open steps.** For the "tap the bubble" and "complete the quest" steps, the [Conversation Panel](conversation-panel.md) ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175)) opens bottom-center; the coach bar sits **just above** the panel so both stay visible.
- **The four step texts** (from `OnboardingSequence`):
    1. **Pan** — "Welcome to Doggiehood! Drag to look around the neighborhood."
    2. **Zoom** — "Nice! Pinch (or scroll) to zoom in and out."
    3. **Tap bubble** — "{Dog} has something to say — tap the speech bubble!"
    4. **Complete** — "Help them out to finish your first quest!"
- **Supersedes the graybox top banner.** This bottom-center coach prompt replaces the old top-banner rendering in `OnboardingOverlay`; [#207](https://github.com/derekwinters/lucas-doggiehood/issues/207) deletes that old top-banner code once this lands.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
