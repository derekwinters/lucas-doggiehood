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

**Regions:** coin token (left) · balance number (right, tabular figures), inside a cream pill.

| Constant | Value | Region |
|---|---|---|
| `HeightPx` | 64 | chip |
| `CoinDiameterPx` | 44 | coin token |
| `PaddingLeftPx` | 10 | coin inset |
| `PaddingRightPx` | 26 | number inset |
| `FontSizePx` | 34 | balance (tabular) |

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
