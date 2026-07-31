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

## Phase-title region (proposed — issue #435)

*Proposal — awaiting approval on [#435](https://github.com/derekwinters/lucas-doggiehood/issues/435). Not yet distilled into the Regions / Anchors & layout constants tables above; nothing implemented.*

The step-dots are misleading now that onboarding spans **four phases** across two state machines — the four-step tutorial, then the three reward-chain steps ([Onboarding reward-chain](../onboarding.md#onboarding-reward-chain-316)) — rather than four individual steps: they freeze at 4/4 through the whole reward chain (see the "Step-dots (resolved)" note above, which this proposal supersedes). This proposal **drops `StepDotCount`/the trailing step-dots region entirely** and replaces it with a **phase-title tab**: an overlapping gold tab at the coach bar's top-left corner, echoing the [DialogueBox name tag](shared-components.md#dialogue-box-dialoguebox) Derek pointed to. It is **styled** like the name tag (same overlap/padding/font proportions, same `Gold` "coin token, name tag" role fill) but is **not a literal component reuse** — the coach bar is drawn procedurally on IMGUI via `CandyChrome`, not the UGUI `DialogueBox` shell the name tag belongs to.

**Regions (supersedes the Trailing step-dots row above):**

| Region | Contains | Shared component |
|---|---|---|
| Phase-title tab | An overlapping tab at the top-left of the coach bar naming the current onboarding **phase** (not step) | Styled like [DialogueBox](shared-components.md#dialogue-box-dialoguebox)'s name tag; drawn via `CandyChrome` |

**Proposed layout constants (supersede `StepDotCount`):**

| Constant | Value | Applies to |
|---|---|---|
| `PhaseTitleAnchor` | `TopLeft` | Tab position, overlapping the coach bar's top edge |
| `PhaseTitleLeftInsetPx` | `34` | Tab inset from the bar's left edge (aligns above the paw badge) |
| `PhaseTitleOffsetPx` | `28` | Vertical overlap above the bar's top edge (matches `NameTagOffsetPx`) |
| `PhaseTitlePaddingXPx` | `30` | Tab horizontal label inset (matches the name tag) |
| `PhaseTitlePaddingYPx` | `8` | Tab vertical label inset (matches the name tag) |
| `PhaseTitleFontPx` | `26` | Tab label size (matches the name tag) |
| `PhaseTitleContentTopPaddingPx` | `48` | Revised again (`/revise`, 2026-07-31): top padding inside the coach bar's content row when the phase-title tab is shown, so the badge/message row clears the tab. The first revision (24px, fixed 112px bar height) assumed a single-line message; the tutorial step-1 message wraps to two lines at `CoachWidthPx`, and the taller wrapped content rode back up into the tab under vertical centering. Fix: the bar's height is no longer fixed in this configuration — it is `max(CoachHeightPx, measured content height) + PhaseTitleContentTopPaddingPx + PhaseTitleContentBottomPaddingPx` (88 + 48 + 16 = 152 for a single-line message; taller still for a wrapped one), so a longer message grows the bar instead of colliding with the tab — consistent with the existing grow-to-fit-then-wrap-and-grow-taller behavior above. `CoachBottomMarginPx` is unaffected — the bar grows upward from its bottom-anchored position. |
| `PhaseTitleContentBottomPaddingPx` | `16` | New (2nd `/revise`, 2026-07-31): bottom padding inside the coach bar's content row when the phase-title tab is shown, so a wrapped 2-line message keeps symmetric breathing room instead of crowding the bar's bottom edge. |

**Per-phase titles (PROPOSAL, for approval — Derek's starting set):**

| Phase | Steps covered | Title |
|---|---|---|
| Tutorial | pan → zoom → tap bubble → complete quest | "Learn the ropes" |
| Upgrade | upgrade a house | "Fix up a home" |
| Expand | expand the map | "Grow the neighborhood" |
| Build | build a house | "Build a house" |

The tab's label swaps once per **phase**, not per step — all four tutorial steps show "Learn the ropes"; each reward-chain step shows its own phase title. Source lives in Core (a per-phase title lookup alongside `OnboardingCoach`'s existing per-step prompt lookup), à la `OnboardingRewardCopy`; the thin `OnboardingOverlay` only renders it.

**Open calls for review:** (1) the tab's placement (top-left, overlapping) and gold fill, modeled on the name tag but not identical component reuse; (2) the four proposed titles themselves — tweak freely; (3) the content-clearance padding (48px top / 16px bottom, content-sized rather than a fixed bar height) — tune if it still looks tight or too loose in the mockup, which now shows the tutorial step-1 message wrapped to two lines as its worst case. See the proposed mockup frame in [mockups/onboarding-overlay.html](mockups/onboarding-overlay.html) for the visual.

## Notes

- **Restyle status ([#297](https://github.com/derekwinters/lucas-doggiehood/issues/297)): implemented (trailing step-dots pending removal — see [Phase-title region, #435](#phase-title-region-proposed-issue-435)).** The coach bar renders the Candy Cottage chrome — a cream pill with a thick Ink outline, a hard straight-down drop-shadow, a round Leaf paw badge, the message text, and a trailing row of outlined step-dots (the current step filled). It is drawn **procedurally on IMGUI** via the shared `CandyChrome` helper (the same runtime white-AA-circle routine established by the HUD chip [#296](https://github.com/derekwinters/lucas-doggiehood/issues/296)), with the message set in the bundled `DejaVuSans` Resources font ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). The bar stays on **IMGUI** rather than migrating to UGUI: the procedural-chrome path avoids the #291 UGUI shader/font-stripping build risk and keeps the coach bar consistent with the HUD. The leaf badge draws a **procedural ink paw-print** (one pad + three toes) rather than the mockup's 🐾 emoji glyph, since the bundled font carries no paw-emoji glyph (it would render as tofu on device). Implemented in `Assets/Scripts/Unity/OnboardingOverlay.cs`; on-device re-playtest is the final visual check.
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
