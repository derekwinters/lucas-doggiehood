# Onboarding overlay

*Wireframe issue: [#176](https://github.com/derekwinters/lucas-doggiehood/issues/176). Implements/covers: `OnboardingOverlay`. Approved: Derek, 2026-07-24 (in-session).*
*Mockup: [mockups/onboarding-overlay.html](mockups/onboarding-overlay.html).*

## Purpose

First-launch guidance ([#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)/[#44](https://github.com/derekwinters/lucas-doggiehood/issues/44)), layered over live gameplay — there is **no blocking modal**. A slim coach prompt sits **bottom-center** and advances itself through the four onboarding steps (pan, zoom, tap the speech bubble, complete the first quest) as the player performs each real action, then auto-dismisses for good. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

The whole overlay is a single **coach bar** — a slim pill floated over the neighborhood. It carries an inline content row plus an overlapping title tab:

| Region | Contains | Shared component |
|---|---|---|
| Phase-title tab | An overlapping gold tab at the coach bar's top-left naming the current onboarding **phase** (not step) — see [Phase-title region](#phase-title-region) | Styled like the [DialogueBox](shared-components.md#dialogue-box-dialoguebox) name tag; drawn procedurally via `CandyChrome` |
| Leading badge | A round paw badge that marks the prompt as the coach's voice | Reuses the Candy Cottage baseline (thick outline, hard shadow, pill) from [Shared UI Components](shared-components.md) / [Art & UI Style](../world/art-style.md) |
| Message text | The current step's guidance text (from the Core `OnboardingSequence`) | — |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `CoachAnchor` | `BottomCenter` | Coach bar position — sits bottom-center so the dog and neighborhood stay visible above it |
| `CoachWidthPx` | `900` | Coach bar width (centered) |
| `CoachHeightPx` | `88` | Coach bar height |
| `CoachBottomMarginPx` | `56` | Gap below the coach bar |
| `MsgFontPx` | `30` | Step message text size |

The coach bar carries a **phase-title tab** — an overlapping gold tab at its top-left corner, styled like the [DialogueBox](shared-components.md#dialogue-box-dialoguebox) name tag — in place of any progress indicator; its constants, per-phase titles, and content-sizing rule live in [Phase-title region](#phase-title-region) below.

The bar's **style** — thick outline, hard drop shadow, pill (999 px) radius — is the shared Candy Cottage baseline from [Shared UI Components](shared-components.md) / [Art & UI Style](../world/art-style.md) and is not re-specified here; this page places the bar and sizes it and its message text.

## Standard onboarding coverage (#374)

*Approved extension: Derek, 2026-07-30 ([`/approve` on #374](https://github.com/derekwinters/lucas-doggiehood/issues/374#issuecomment-5125178403)) — authoritative. This coach bar is the **one standard guidance surface** for **all** onboarding steps — not just the first four — so no new prompt design is invented per flow.*

- **Coverage across the whole guided journey.** The same coach bar now guides the [onboarding reward-chain](../onboarding.md#onboarding-reward-chain-316) steps ([#316](https://github.com/derekwinters/lucas-doggiehood/issues/316)) as well as the original four: after the first quest it re-appears for **upgrade a house → expand the map → build a house**, advancing on each real action, then dismisses for good when the chain completes (implementation [#371](https://github.com/derekwinters/lucas-doggiehood/issues/371)). **Added step copy** (accepted defaults, `/approve` #374):
    5. **Upgrade a house** — "Tap a house, then Upgrade to make it even nicer!"
    6. **Expand the map** — "Tap the glowing lock to open up a new street!"
    7. **Build a house** — "Tap the empty lot to build a new house!"
- **Grow-to-fit width (folds in [#369](https://github.com/derekwinters/lucas-doggiehood/issues/369)).** `CoachWidthPx` (900) becomes a **minimum**, not a fixed width: the bar sizes its width to the measured message (plus the paw badge and paddings), clamped to a `CoachMaxWidthPx` beyond which the text **wraps** to a second line and the bar grows in height instead — so no step string ever overflows the pill or runs off-screen. Bottom-center anchor and the panel-open behavior are unchanged. When wrapping, the bar height grows by one `MsgFontPx` line + existing vertical padding.

    **Locked #374 constants** (these are the values [#371](https://github.com/derekwinters/lucas-doggiehood/issues/371) declares and EditMode-tests against, per [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161)'s no-inline-literals rule):

    | Constant | Value | Applies to |
    |---|---|---|
    | `CoachWidthPx` | `900` | Now a **minimum** width (was a fixed width); the bar grows to fit the measured message |
    | `CoachMaxWidthPx` | `1500` | Max width before the message **wraps** and the bar grows in height instead |
- **Completion hands off to the reward panel.** Each step's completion raises the bespoke [onboarding reward panel](onboarding-reward.md) ([#374](https://github.com/derekwinters/lucas-doggiehood/issues/374)) celebrating the step + its coin reward; the coach bar then shows the next step's prompt (or dismisses after the last).
- **Coverage status ([#371](https://github.com/derekwinters/lucas-doggiehood/issues/371)): implemented.** The coach bar re-shows for the three reward-chain steps with the accepted copy above, advancing on each **real action** — upgrade a house / unlock a zone / build a house — observed on `GameState.RewardChain`, and dismisses for good once the chain completes at the build step (releasing normal quest rotation, the [#312](https://github.com/derekwinters/lucas-doggiehood/issues/312)→[#310](https://github.com/derekwinters/lucas-doggiehood/issues/310) handoff). The engine-free decision — the per-step prompt lookup, the per-phase title lookup, and the "keep showing until the chain completes" dismissal gate — lives in Core `OnboardingCoach`; the thin `OnboardingOverlay` renders it. The grow-to-fit width (`CoachWidthPx` minimum, `CoachMaxWidthPx` wrap) and the [phase-title tab](#phase-title-region) are wired against the Locked #374 constants above and the phase-title constants below. (The reward panel itself is [#372](https://github.com/derekwinters/lucas-doggiehood/issues/372)'s job, not wired here.)

## Phase-title region

*Approved: Derek, 2026-07-31 ([`/approve` on #435](https://github.com/derekwinters/lucas-doggiehood/issues/435#issuecomment-5148389570)). Replaces the earlier trailing step-dots region: the dot count tracked only the first-launch four steps and froze at 4/4 through the reward chain, so it was misleading now that onboarding spans four **phases** across two state machines. `StepDotCount`/the step-dots region are **dropped entirely**.*

Instead of a progress indicator, the coach bar names the current onboarding **phase** with a **phase-title tab**: an overlapping gold tab at the bar's top-left corner, echoing the [DialogueBox name tag](shared-components.md#dialogue-box-dialoguebox). It is **styled** like the name tag (same overlap/padding/font proportions, same `Gold` "coin token, name tag" role fill) but is **not** a literal component reuse — the coach bar is drawn procedurally on IMGUI via `CandyChrome`, not the UGUI `DialogueBox` shell the name tag belongs to.

**Layout constants** (authored at the 1920×1200 reference; named per [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161)):

| Constant | Value | Applies to |
|---|---|---|
| `PhaseTitleAnchor` | `TopLeft` | Tab position, overlapping the coach bar's top edge |
| `PhaseTitleLeftInsetPx` | `34` | Tab inset from the bar's left edge (aligns above the paw badge) |
| `PhaseTitleOffsetPx` | `28` | Vertical overlap above the bar's top edge (matches `NameTagOffsetPx`) |
| `PhaseTitlePaddingXPx` | `30` | Tab horizontal label inset (matches the name tag) |
| `PhaseTitlePaddingYPx` | `8` | Tab vertical label inset (matches the name tag) |
| `PhaseTitleFontPx` | `26` | Tab label size (matches the name tag) |
| `PhaseTitleContentTopPaddingPx` | `48` | Top padding inside the coach bar's content row when the phase-title tab is shown, so the badge/message row clears the tab |
| `PhaseTitleContentBottomPaddingPx` | `16` | Bottom padding inside the content row, keeping a wrapped two-line message symmetric instead of crowding the bar's bottom edge |

**Content-sized bar (clears the tab at any message length).** When the phase-title tab is shown, the bar's height is **not** fixed: it is `max(CoachHeightPx, measured content height) + PhaseTitleContentTopPaddingPx + PhaseTitleContentBottomPaddingPx` (88 + 48 + 16 = 152 for a single-line message; taller for a wrapped one). A longer or wrapped message grows the bar instead of colliding with the tab — the same "wrap and grow taller" behavior as the grow-to-fit width above, composed with the tab's clearance. `CoachBottomMarginPx` is unaffected: the bar grows **upward** from its bottom-anchored position. (The worst case is the tutorial step-1 message, which wraps to two lines at `CoachWidthPx`.)

**Per-phase titles.** The tab's label swaps once per **phase**, not per step — all four tutorial steps show "Learn the ropes"; each reward-chain step shows its own phase title:

| Phase | Steps covered | Title |
|---|---|---|
| Tutorial | pan → zoom → tap bubble → complete quest | "Learn the ropes" |
| Upgrade | upgrade a house | "Fix up a home" |
| Expand | expand the map | "Grow the neighborhood" |
| Build | build a house | "Build a house" |

Source lives in Core (a per-phase title lookup alongside `OnboardingCoach`'s existing per-step prompt lookup), à la `OnboardingRewardCopy`; the thin `OnboardingOverlay` only renders it.

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

- **Restyle status ([#297](https://github.com/derekwinters/lucas-doggiehood/issues/297)): implemented (including the [phase-title tab](#phase-title-region), [#451](https://github.com/derekwinters/lucas-doggiehood/issues/451)).** The coach bar renders the Candy Cottage chrome — a cream pill with a thick Ink outline, a hard straight-down drop-shadow, a round Leaf paw badge, and the message text; the top-left gold [phase-title tab](#phase-title-region) replaces the former trailing step-dots (which are dropped entirely, along with `StepDotCount`/`DrawStepDots`). It is drawn **procedurally on IMGUI** via the shared `CandyChrome` helper (the same runtime white-AA-circle routine established by the HUD chip [#296](https://github.com/derekwinters/lucas-doggiehood/issues/296)), with the message set in the bundled `DejaVuSans` Resources font ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). The bar stays on **IMGUI** rather than migrating to UGUI: the procedural-chrome path avoids the #291 UGUI shader/font-stripping build risk and keeps the coach bar consistent with the HUD. The leaf badge draws a **procedural ink paw-print** (one pad + three toes) rather than the mockup's 🐾 emoji glyph, since the bundled font carries no paw-emoji glyph (it would render as tofu on device). Implemented in `Assets/Scripts/Unity/OnboardingOverlay.cs`; on-device re-playtest is the final visual check.
- **Runs once, over live gameplay.** The sequence runs only on first launch (driven by the Core `OnboardingSequence`) and never blocks input — it floats over the real neighborhood, real dog, real quest, and real reward, not a scripted tutorial scene.
- **Advances on the real action.** Each step advances automatically when the player performs the actual action — pan, then zoom, then tap the speech bubble, then complete the quest — and the overlay auto-dismisses after step 4.
- **Panel-open steps.** For the "tap the bubble" and "complete the quest" steps, the [Conversation Panel](conversation-panel.md) ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175)) opens bottom-center; the coach bar sits **just above** the panel so both stay visible. That "sit above" stacking only works because the conversation panel is itself bottom-anchored.
- **Centered modal panels hide the coach bar** ([#506](https://github.com/derekwinters/lucas-doggiehood/issues/506)). A **center-anchored** modal panel — the [house profile](house-profile.md) (whose footer **Upgrade** button the "fix up a home" step tells the player to tap) and the [dog profile](dog-profile.md), plus any future centered panel — cannot be cleanly stacked with the bottom-center bar the way the conversation panel is: its variable-height card lands in the same band the coach bar occupies, so the bar would cover the very control it points at. While any centered modal panel is open the coach bar is therefore **suppressed outright** (not repositioned), and re-shown unchanged when the panel closes. Suppression is a pure visibility rule — it never advances or skips the onboarding step, and a coach bar already dismissed for good (the reward chain complete) stays dismissed regardless. The decision lives in Core (`OnboardingCoach.ShouldShow`'s `centeredPanelOpen` argument); `WorldBootstrap` wires the "is a centered panel open" observation into `OnboardingOverlay`, composed over every centered panel so it isn't special-cased to houses. **Trade-off (accepted):** the player loses the on-screen "Tap a house, then Upgrade…" reminder for the moment the profile is open — the same moment they're looking straight at the Upgrade button. **Filled by the target-house highlight ([#571](https://github.com/derekwinters/lucas-doggiehood/issues/571)).** That gap is covered by a persistent world-space red ground-ring highlight on the target house during the "fix up a home" (upgrade) step — a *pointer here, not a new region of this overlay*: it is non-interactive gameplay VFX attached to the house (the same category as the lost-item finder glow and the bug-swarm marker), reusing the [#535](https://github.com/derekwinters/lucas-doggiehood/issues/535) red ring rather than adding screen-space chrome, and it is deliberately **not** subject to this coach-bar suppression (that non-collision with the centered panel is exactly why it fills the gap). See [Onboarding → target-house highlight](../onboarding.md#onboarding-reward-chain-316).
- **The four step texts** (from `OnboardingSequence`):
    1. **Pan** — "Welcome to Doggiehood! Drag to look around the neighborhood."
    2. **Zoom** — "Nice! Pinch (or scroll) to zoom in and out."
    3. **Tap bubble** — "{Dog} has something to say — tap the speech bubble!"
    4. **Complete** — "Help them out to finish your first quest!"
- **Supersedes the graybox top banner.** This bottom-center coach prompt replaces the old top-banner rendering in `OnboardingOverlay`; [#207](https://github.com/derekwinters/lucas-doggiehood/issues/207) removed that old top-banner code, laid the bar out against the constants above, and fixed the prompt so it advances on the real interactions and auto-dismisses after the first quest.
- **No-camera fallback.** If no `CameraRig` is present when onboarding starts (a degenerate case — the shipped scene always has one), the pan and zoom steps have nothing to act on, so they are treated as satisfied rather than deadlocking the sequence; onboarding still advances through the tap and complete steps and dismisses.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
