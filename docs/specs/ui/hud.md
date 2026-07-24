# HUD

*Wireframe issue: [#174](https://github.com/derekwinters/lucas-doggiehood/issues/174). Implements/covers: `HudOverlay`. Approved: Derek, 2026-07-23 (in-session).*
*Mockup: [mockups/hud.html](mockups/hud.html).*

## Purpose

The persistent heads-up layer drawn over the neighborhood on the primary target — a landscape tablet (device #22, 1920×1200 reference; see [Overview](index.md)). It is always on screen during play. Today it carries exactly one element, the currency chip; the layout reserves room for future HUD elements without re-deciding the chip's placement.

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Currency chip | The player's coin balance (coin token + tabular number), pinned to the top-right corner clear of the safe area | [CurrencyChip](shared-components.md) |
| (reserved) future HUD elements | Nothing today — space intentionally left for later persistent HUD additions, so adding one does not disturb the chip's anchor | — |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `HudChipAnchor` | `TopRight` | Corner the currency chip pins to |
| `HudEdgeMarginPx` | `36` | Inset from the safe-area top and right edges to the chip |

The chip's own size (`CurrencyChip.HeightPx` = 64, `CurrencyChip.CoinDiameterPx` = 44, etc.) is owned by the shared [CurrencyChip](shared-components.md) component ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) and is not re-specified here — this page only places that component.

## Notes

- **Retroactive coverage.** This wireframe retrofits the already-shipped `HudOverlay` chip. It keeps the shipped **top-right** anchor, but supersedes the graybox 140×32 `GUI.Box` with the real [CurrencyChip](shared-components.md) and measures the inset from the **safe-area** edges rather than the raw screen edge. The old top banner is being removed ([#207](https://github.com/derekwinters/lucas-doggiehood/issues/207)).
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md) ([#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
