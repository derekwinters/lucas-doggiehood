# Shared UI Components

*Wireframe issue: [#173](https://github.com/derekwinters/lucas-doggiehood/issues/173) (stub established in [#172](https://github.com/derekwinters/lucas-doggiehood/issues/172)). Style source: [Art & UI Style](../world/art-style.md) ([#65](https://github.com/derekwinters/lucas-doggiehood/issues/65)).*
*Mockup: [mockups/shared-components.html](mockups/shared-components.html).*

**Reference resolution: 1920×1200 (16:10 tablet, landscape — [#22](https://github.com/derekwinters/lucas-doggiehood/issues/22)).** Every layout constant on this page is authored against that reference; a Unity `CanvasScaler` scales from it. See the [target platform note](index.md#target-platform-reference-resolution).

Atomic, reusable UI pieces are documented **once, here**, and referenced by every per-screen [wireframe](index.md) that uses them — never re-specified per screen. This page is the shared reference: a screen's page names a component and points here, rather than restating its shape, outline, or shadow.

The **style** of every component below comes from the "Candy Cottage" direction settled in [Art & UI Style](../world/art-style.md). This page pulls the rules that apply to *every* component forward as the shared baseline; the full rationale and the reference mockup live on that page. What this page adds on top of style is **layout**: each component's regions and its named layout constants (per [#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)), which implementation declares and EditMode tests assert against.

## Shared style baseline (Candy Cottage)

Every UI component inherits, from [Art & UI Style](../world/art-style.md):

- **Thick dark outlines** on all chrome.
- **Flat, hard drop-shadows** — no blur.
- **Chunky pill / rounded shapes** — pill-shaped buttons and chips.
- **Bold rounded sans-serif type.**

A sticker-book feel, chosen for legibility against the bright/saturated palette and low-poly toy-shelf look.

### Shared baseline constants

Inherited by every component; a component's own table adds only what is specific to it.

| Constant | Value | Applies to |
|---|---|---|
| `OutlineThicknessPx` | 6 | dark outline on all chrome |
| `ShadowOffsetPx` | 8 | hard drop-shadow, straight down, no blur |
| `PillRadiusPx` | 999 | buttons & chips (full pill) |
| `PanelRadiusPx` | 40 | dialogue box / panel chrome |

These baseline constants + the palette below are realized in code from this single source (no per-screen hand-picked values, [#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)): the IMGUI overlays draw them via `CandyChrome` (HUD chip [#296](https://github.com/derekwinters/lucas-doggiehood/issues/296), onboarding), and the retained-UGUI panels via `CandyChromeUgui` (Settings [#298](https://github.com/derekwinters/lucas-doggiehood/issues/298)) — both procedural, no raster art asset.

### Shared palette

The fixed Candy Cottage component colors (the same values regardless of viewer theme).

| Name | Hex | Typical role |
|---|---|---|
| Ink | `#2E2A26` | outlines, shadows, text on light fills |
| Cream | `#FFF3D9` | neutral / decline fills |
| Panel | `#FFFDF7` | dialogue box / panel chrome fill |
| Coral | `#FF7A5C` | primary / spend fills |
| Leaf | `#58C06A` | positive / confirm fills |
| Gold | `#FFC23C` | coin token, name tag |
| Disabled | `#D8D2C6` | disabled fills (outline + text dim together) |

**Role-tint mapping** (used by the pill button and any tinted chrome): Positive = leaf, Primary/spend = coral, Neutral/decline = cream, Disabled = grey (outline and text dim together).

## Components

Each component's subsection gives its regions and its **named layout constants**, corresponding 1:1 with the [mockup](mockups/shared-components.html). Screens reuse these by name; they do not redefine them. Style itself is sourced from [Art & UI Style](../world/art-style.md) — the tables below are layout/constants only.

### Pill button (`PillButton`)

Decoration/gift choices, dialogue actions, menu buttons. One shape, tinted by role. Pressing collapses the shadow and drops the button down by `ShadowOffsetPx`.

**Regions:** label (optional leading icon) inside a single pill; role tint applied to the fill.

| Constant | Value | Region |
|---|---|---|
| `HeightPx` | 96 | tap target |
| `PaddingXPx` | 48 | label inset |
| `FontSizePx` | 36 | label |
| `IconGapPx` | 16 | icon → label |

Role tints: Positive = leaf, Primary/spend = coral, Neutral/decline = cream, Disabled = grey (outline + text dim together).

### Currency chip (`CurrencyChip`)

The HUD coin indicator — a coin token plus the live balance. Sits in a screen corner (its anchor is fixed in the HUD wireframe, [#174](https://github.com/derekwinters/lucas-doggiehood/issues/174)).

*Status: **implemented** in the HUD ([#296](https://github.com/derekwinters/lucas-doggiehood/issues/296)) — `Assets/Scripts/Unity/HudOverlay.cs` draws the cream pill, Ink outline, hard drop-shadow, and gold coin token to the constants below; the balance is the bare tabular number (the coin token supplies the "coins" meaning). Sized to match the Settings gear beside it ([#440](https://github.com/derekwinters/lucas-doggiehood/issues/440)): the chip height equals `GearButtonSizePx` (88) and the interior is scaled ×1.375 to fill the taller pill.*

**Regions:** coin token (left) · balance number (right, tabular figures) · **floating delta label** (transient, appears below the chip on a balance change), inside a cream pill.

| Constant | Value | Region |
|---|---|---|
| `HeightPx` | 88 | chip (matches the Settings gear) |
| `CoinDiameterPx` | 60 | coin token |
| `PaddingLeftPx` | 14 | coin inset |
| `PaddingRightPx` | 36 | number inset |
| `FontSizePx` | 46 | balance (tabular) |
| `DeltaFontSizePx` | 32 | delta label text |
| `DeltaOffsetYPx` | 12 | gap: chip bottom edge → delta label start |
| `DeltaRiseDistancePx` | 48 | total rise distance before the label is discarded |
| `DeltaRiseDurationSec` | 0.9 | delta rise + fade duration |
| `CountUpDurationSec` | 0.5 | balance-number count-up (tween) duration |

**Balance-change animation ([#542](https://github.com/derekwinters/lucas-doggiehood/issues/542)).** On a `Wallet.Coins` change the chip spawns a **delta label** showing the signed amount — `+123` in **Leaf** (the palette's positive/confirm role) on a gain, `−45` in **Coral** (primary/spend) on a spend — starting `DeltaOffsetYPx` below the chip and centred under it. It rises `DeltaRiseDistancePx` while fading linearly to transparent over `DeltaRiseDurationSec`, then is discarded (no new colors are introduced — the delta reuses the shared palette). Independently, the displayed balance **counts up** from its prior displayed value to the new live `Wallet.Coins` value over `CountUpDurationSec` rather than snapping; a second change before the count-up finishes just re-targets it toward the newer value (from wherever it currently reads) rather than queuing. Each change spawns its own delta-label instance — there is no stacking/queue model for the label (unlike the [reward toast](toast.md), [#541](https://github.com/derekwinters/lucas-doggiehood/issues/541), which carries the *message*; this chip carries only the *running-total* motion). Purely decorative: it does not register with `ModalInputGate` and never blocks a tap. The count-up + rise/fade math is Unity-independent (`Doggiehood.Core.Economy.CoinChipAnimation`, plain-NUnit tested); `HudOverlay` only reads it each frame and paints it.

### Speech-bubble indicator (`SpeechBubbleIndicator`)

Floats over a dog that has something to say; tapping it opens the conversation. Bobs gently to draw the eye (motion only — the layout is the static bubble + tail).

**Regions:** round bubble body · downward tail (pointer) · three "…" dots inside.

| Constant | Value | Region |
|---|---|---|
| `DiameterPx` | 104 | bubble |
| `TailSizePx` | 24 | pointer |
| `DotDiameterPx` | 13 | "…" glyph ×3 |
| `BobAmplitudePx` | 8 | idle motion |

### Dialogue box (`DialogueBox`)

The shared panel **shell** for conversation ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175)) and onboarding ([#176](https://github.com/derekwinters/lucas-doggiehood/issues/176)) surfaces — name tag, body, action row. Screens fill the content; the chrome is defined once here.

**Regions:** name tag (overlapping tab at top) · body text · action row. The specific actions per screen (e.g. whether a decline button exists) are decided in that screen's wireframe, not here.

| Constant | Value | Region |
|---|---|---|
| `PaddingPx` | 40 | panel inset |
| `PanelRadiusPx` | 40 | panel corners |
| `PanelShadowPx` | 12 | drop-shadow |
| `NameTagOffsetPx` | 28 | tab overlap at top |
| `ActionGapPx` | 20 | between buttons |

The overall panel **width and placement** are settled per screen ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175)), not here; the mockup shows a representative wide tablet panel.

## Modal overlays block world input

Every dialog/menu overlay is **modal**: a tap that lands on it — on a button, on the panel's empty area, or on its dimmed backdrop — is consumed by the UI and **never** reaches a world interactable (a dog, a house, a lost item) behind it ([#422](https://github.com/derekwinters/lucas-doggiehood/issues/422)). Tapping **Accept** only accepts; tapping **Close** only closes; neither also fires whatever sat behind the button.

This is why every overlay (confirmation dialog, house profile, dog profile, the conversation panel) carries a **full-screen raycast-blocking scrim/backdrop** under its card. A new modal surface must include such a backdrop, or taps on its empty regions would fall through to the world.

The world tap-router (see [Camera & controls → Navigation](../world/camera-controls.md#navigation)) enforces the block two ways:

- **A shared modal registry (`ModalInputGate`, [#544](https://github.com/derekwinters/lucas-doggiehood/issues/544)).** Every center-anchored overlay — confirmation dialog, dog profile, house profile, welcome pop-up, onboarding reward — registers itself as open on show and unregisters on close (and again if it is destroyed while open, so a torn-down overlay never leaves the block set). While any is registered the tap-router swallows the tap before its world raycast. The registry also exposes a one-call reset used at hard boundaries — scene unload, or test isolation — so a stale registration can never leak past that boundary and leave world taps dead. This is a deterministic, frame-timing-independent flag: it is the authoritative guard, added because the original `EventSystem.IsPointerOverGameObject` signal alone could miss a fast **touch** tap-release over the scrim and leak that tap to the world object behind the panel (opening it and closing the profile).
- **`EventSystem.IsPointerOverGameObject`** on the backdrop remains as a belt-and-suspenders check for overlays not (yet) on the registry.

Blocking suppresses only the *world pass-through*. A scrim/backdrop's own close/cancel button still fires through UGUI, so a panel that intentionally **dismisses on scrim tap** keeps doing so — "dismiss this panel" and "pass the tap through to the world" stay distinct.

The one still-IMGUI affordance, the HUD Settings gear, sits outside the EventSystem and so is covered by an interim screen-space rect guard instead; it folds into this same rule once it migrates to UGUI ([#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)).
