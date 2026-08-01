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

## Standard onboarding coverage (#374)

*Approved extension: Derek, 2026-07-30 ([`/approve` on #374](https://github.com/derekwinters/lucas-doggiehood/issues/374#issuecomment-5125178403)) — authoritative. This coach bar is the **one standard guidance surface** for **all** onboarding steps — not just the first four — so no new prompt design is invented per flow.*

- **Coverage across the whole guided journey.** The same coach bar now guides the [onboarding reward-chain](../onboarding.md#onboarding-reward-chain-316) steps ([#316](https://github.com/derekwinters/lucas-doggiehood/issues/316)) as well as the original four: after the first quest it re-appears for **upgrade a house → expand the map → build a house**, advancing on each real action, then dismisses for good when the chain completes (implementation [#371](https://github.com/derekwinters/lucas-doggiehood/issues/371)). **Added step copy** (accepted defaults, `/approve` #374):
    5. **Upgrade a house** — "Tap a house, then Upgrade to make it even nicer!"
    6. **Expand the map** — "Tap the glowing lock to open up a new street!"
    7. **Build a house** — "Tap the empty lot to build a new house!"
- **Grow-to-fit width (folds in [#369](https://github.com/derekwinters/lucas-doggiehood/issues/369)).** `CoachWidthPx` (900) becomes a **minimum**, not a fixed width: the bar sizes its width to the measured message (plus the paw badge, step-dots, and paddings), clamped to a `CoachMaxWidthPx` beyond which the text **wraps** to a second line and the bar grows in height instead — so no step string ever overflows the pill or runs off-screen. Bottom-center anchor and the panel-open behavior are unchanged. When wrapping, the bar height grows by one `MsgFontPx` line + existing vertical padding.

    **Locked #374 constants** (these are the values [#371](https://github.com/derekwinters/lucas-doggiehood/issues/371) declares and EditMode-tests against, per [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161)'s no-inline-literals rule):

    | Constant | Value | Applies to |
    |---|---|---|
    | `CoachWidthPx` | `900` | Now a **minimum** width (was a fixed width); the bar grows to fit the measured message |
    | `CoachMaxWidthPx` | `1500` | Max width before the message **wraps** and the bar grows in height instead |
    | `StepDotCount` | `4` | Unchanged; dots track the first-launch four steps only (see step-dots note below) |
- **Step-dots (resolved — accepted default).** `StepDotCount` stays `4`: the progress dots track only the first-launch four steps and are **dropped** for the post-first-quest reward-chain steps (upgrade → expand → build), matching the approved mockup's original four-dot bar. (This was flagged as an open sub-decision in the proposal; the bare `/approve` accepts the mockup's default — flagged for Derek to confirm in the distilling PR.)
- **Completion hands off to the reward panel.** Each step's completion raises the bespoke [onboarding reward panel](onboarding-reward.md) ([#374](https://github.com/derekwinters/lucas-doggiehood/issues/374)) celebrating the step + its coin reward; the coach bar then shows the next step's prompt (or dismisses after the last).
- **Coverage status ([#371](https://github.com/derekwinters/lucas-doggiehood/issues/371)): implemented.** The coach bar re-shows for the three reward-chain steps with the accepted copy above, advancing on each **real action** — upgrade a house / unlock a zone / build a house — observed on `GameState.RewardChain`, and dismisses for good once the chain completes at the build step (releasing normal quest rotation, the [#312](https://github.com/derekwinters/lucas-doggiehood/issues/312)→[#310](https://github.com/derekwinters/lucas-doggiehood/issues/310) handoff). The engine-free decision — the per-step prompt lookup and the "keep showing until the chain completes" dismissal gate — lives in Core `OnboardingCoach`; the thin `OnboardingOverlay` renders it. The grow-to-fit width (`CoachWidthPx` minimum, `CoachMaxWidthPx` wrap) and the frozen four-step dots (they stay filled at 4/4 through the reward chain, not advancing) are wired against the Locked #374 constants above. (The reward panel itself is [#372](https://github.com/derekwinters/lucas-doggiehood/issues/372)'s job, not wired here.)

## Gesture-arrow coach ([#330](https://github.com/derekwinters/lucas-doggiehood/issues/330))

*Approved: Derek, 2026-07-31 ([`/approve` on #330](https://github.com/derekwinters/lucas-doggiehood/issues/330#issuecomment-5148157973)). **Status: distilled** (was Proposed) — implemented in the same PR.*

Because this is a kids' game, the two **movement** steps *show* the gesture, not just tell it: a looping directional-arrow coach layers over the map during the **Pan** and **Zoom** steps only (the `TapBubble`/`CompleteQuest` steps are unchanged). The arrows are a non-blocking visual coach — Candy Cottage chevron-arrows with a thick Ink outline and **Gold** fill (a color the coach bar's own chrome does not use, so it reads as "look here, do this") — drawn procedurally via the same `CandyChrome` IMGUI routines as the rest of the overlay. There is **no new rendering path** and no separate soft-lock: the arrows draw only while `OnboardingOverlay.ShouldDrawGesture` is true (the sequence is on `Pan`/`Zoom`) and vanish the instant `OnboardingOverlay.AdvanceCameraSteps` registers the real pan/zoom, the same real-action gate that advances the coach bar.

- **Pan step — 4-beat loop.** A single directional arrow cycles **left→right → right→left → up→down → down→up**, its center sweeping `PanTravelPx` along the beat's axis over `BeatDurationSec`, holding `BeatPauseSec` between beats, then repeating.
- **Zoom step — 2-beat loop.** A symmetric pair of arrows either side of the anchor cycles **zoom in** (start near the center at `ZoomNearOffsetPx` pointing outward, spread to `ZoomFarOffsetPx`) and **zoom out** (start far at `ZoomFarOffsetPx` pointing inward, close to `ZoomNearOffsetPx`), on the same beat timing.
- **Placed generously.** The group is anchored at screen-center-x, `GestureCenterYPx` down from the top — clear of the bottom coach bar and the HUD. During `Pan`/`Zoom` the target dog's speech bubble is suppressed ([#329](https://github.com/derekwinters/lucas-doggiehood/issues/329)), so no interactive element sits under the arrows.

**Regions**

| Region | Contains | Shared component |
|---|---|---|
| Pan gesture arrow | One directional chevron-arrow cycling the four pan beats | Ink outline / hard shadow baseline, [Shared UI Components](shared-components.md) |
| Zoom gesture arrows | A symmetric pair of chevron-arrows either side of the anchor, cycling the two zoom beats | Ink outline / hard shadow baseline, [Shared UI Components](shared-components.md) |

**Anchors & layout constants** (authored at the 1200px reference; declared as named constants per [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161))

| Constant | Value | Applies to |
|---|---|---|
| `GestureCenterYPx` | `480` | vertical anchor of the gesture group (horizontal = screen center) |
| `ArrowLengthPx` | `200` | each arrow's shaft + head, along its axis |
| `ArrowThicknessPx` | `22` | shaft width |
| `ArrowHeadSizePx` | `56` | chevron arrowhead span |
| `ArrowOutlineThicknessPx` | `6` | ink outline (matches the shared `OutlineThicknessPx` baseline) |
| `PanTravelPx` | `260` | distance the pan arrow's center sweeps per beat |
| `ZoomNearOffsetPx` | `70` | each zoom arrow's distance from the anchor, closest |
| `ZoomFarOffsetPx` | `220` | each zoom arrow's distance from the anchor, farthest |
| `BeatDurationSec` | `1.1` | one sweep/spread animation (Core `GestureCoach`) |
| `BeatPauseSec` | `0.5` | hold between beats (Core `GestureCoach`) |
| `ArrowFillOpacity` | `0.92` | keeps the map faintly readable under the arrow |

**Core/Unity split.** The beat sequencer is engine-free Core (`GestureCoach.BeatAt`): it maps elapsed time + the current `OnboardingStep` to the active `GestureBeat` (`LeftToRight`/`RightToLeft`/`UpToDown`/`DownToUp` for Pan; `ZoomIn`/`ZoomOut` for Zoom; `Hidden` for every other step) and 0–1 sweep progress within `BeatDurationSec`/`BeatPauseSec` (held at 1 through the pause). The thin `OnboardingOverlay` turns each beat + progress into arrow screen offsets via public static methods (`ComputePanArrowCenter`, `ComputeZoomArrowOffsetPx`/`ComputeZoomArrowCenters`, mirroring `ComputeCoachRect`) and draws them; `BeatDurationSec`/`BeatPauseSec` are the timing constants, the pixel constants above are the on-screen geometry.

**Decisions (from the approved proposal).**
1. **Gold fill**, not Cream/Leaf — keeps the gesture cue distinct from the coach bar's chrome so it reads as an action prompt.
2. **4 separate beats for pan** (not one bidirectional double-headed arrow) — matches the issue text literally and keeps each beat unambiguous for a young player.
3. **`GestureCenterYPx` fixed at 480**, not tied to the target dog's live position — the bubble is suppressed during Pan/Zoom anyway (#329), so a fixed, generous anchor avoids coupling this layout to gameplay state.

## Notes

- **Restyle status ([#297](https://github.com/derekwinters/lucas-doggiehood/issues/297)): implemented.** The coach bar renders the Candy Cottage chrome — a cream pill with a thick Ink outline, a hard straight-down drop-shadow, a round Leaf paw badge, the message text, and a trailing row of outlined step-dots (the current step filled). It is drawn **procedurally on IMGUI** via the shared `CandyChrome` helper (the same runtime white-AA-circle routine established by the HUD chip [#296](https://github.com/derekwinters/lucas-doggiehood/issues/296)), with the message set in the bundled `DejaVuSans` Resources font ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). The bar stays on **IMGUI** rather than migrating to UGUI: the procedural-chrome path avoids the #291 UGUI shader/font-stripping build risk and keeps the coach bar consistent with the HUD. The leaf badge draws a **procedural ink paw-print** (one pad + three toes) rather than the mockup's 🐾 emoji glyph, since the bundled font carries no paw-emoji glyph (it would render as tofu on device). Implemented in `Assets/Scripts/Unity/OnboardingOverlay.cs`; on-device re-playtest is the final visual check.
- **Runs once, over live gameplay.** The sequence runs only on first launch (driven by the Core `OnboardingSequence`) and never blocks input — it floats over the real neighborhood, real dog, real quest, and real reward, not a scripted tutorial scene.
- **Advances on the real action.** Each step advances automatically when the player performs the actual action — pan, then zoom, then tap the speech bubble, then complete the quest — and the overlay auto-dismisses after step 4.
- **Panel-open steps.** For the "tap the bubble" and "complete the quest" steps, the [Conversation Panel](conversation-panel.md) ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175)) opens bottom-center; the coach bar sits **just above** the panel so both stay visible.
- **The four step texts** (from `OnboardingSequence`):
    1. **Pan** — "Welcome to Doggiehood! Drag to look around the neighborhood."
    2. **Zoom** — "Nice! Pinch (or scroll) to zoom in and out."
    3. **Tap bubble** — "{Dog} has something to say — tap the speech bubble!"
    4. **Complete** — "Help them out to finish your first quest!"
- **Supersedes the graybox top banner.** This bottom-center coach prompt replaces the old top-banner rendering in `OnboardingOverlay`; [#207](https://github.com/derekwinters/lucas-doggiehood/issues/207) removed that old top-banner code, laid the bar out against the constants above, and fixed the prompt so it advances on the real interactions and auto-dismisses after the first quest.
- **No-camera fallback.** If no `CameraRig` is present when onboarding starts (a degenerate case — the shipped scene always has one), the pan and zoom steps have nothing to act on, so they are treated as satisfied rather than deadlocking the sequence; onboarding still advances through the tap and complete steps and dismisses.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
