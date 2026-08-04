# HUD

*Wireframe issue: [#174](https://github.com/derekwinters/lucas-doggiehood/issues/174). Implements/covers: `HudOverlay`. Approved: Derek, 2026-07-23 (in-session).*
*Mockup: [mockups/hud.html](mockups/hud.html).*
*Status: **implemented** — the graybox chip was restyled to the full Candy Cottage [CurrencyChip](shared-components.md) chrome in [#296](https://github.com/derekwinters/lucas-doggiehood/issues/296) (`Assets/Scripts/Unity/HudOverlay.cs`, IMGUI).*

## Purpose

The persistent heads-up layer drawn over the neighborhood on the primary target — a landscape tablet (device #22, 1920×1200 reference; see [Overview](index.md)). It is always on screen during play. Today it carries exactly one element, the currency chip; the layout reserves room for future HUD elements without re-deciding the chip's placement.

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Currency chip | The player's coin balance (coin token + tabular number), pinned to the top-right corner clear of the safe area | [CurrencyChip](shared-components.md) |
| Toast lane | A reserved **top-left** lane the world/controls never occupy — where a single [toast notification](toast.md) slides in and out. Mirrors the currency chip's top-right reservation in the opposite corner; empty when no toast is showing | [Toast](toast.md) |
| (reserved) future HUD elements | Nothing today — space intentionally left for later persistent HUD additions, so adding one does not disturb the chip's or the toast lane's anchor | — |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `HudChipAnchor` | `TopRight` | Corner the currency chip pins to |
| `HudEdgeMarginPx` | `36` | Legacy chip top inset — superseded by [#440](https://github.com/derekwinters/lucas-doggiehood/issues/440) (the chip now shares the gear's vertical centreline); retained as a named constant |
| `HudToastLaneAnchor` | `TopLeft` | Corner the reserved [toast lane](toast.md) pins to — mirrors `HudChipAnchor` in the opposite corner |
| `HudToastLaneTopMarginPx` | `32` | Toast lane top inset — shares the chip/gear row so both top corners read as one HUD band |
| `HudToastLaneLeftMarginPx` | `36` | Toast lane left inset (safe-area edge) |

The chip's own size (`CurrencyChip.HeightPx` = 88, `CurrencyChip.CoinDiameterPx` = 60, etc.) is owned by the shared [CurrencyChip](shared-components.md) component ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) and is not re-specified here — this page only places that component. The chip height matches the Settings gear beside it ([#440](https://github.com/derekwinters/lucas-doggiehood/issues/440)). The toast lane's own size (`ToastHeightPx` = 88, `ToastMaxWidthPx` = 640, etc.) and its dismiss/queue behavior are owned by the [Toast](toast.md) wireframe ([#562](https://github.com/derekwinters/lucas-doggiehood/issues/562)) — this page only reserves the lane; the lane margins here match the toast's own `ToastLaneTopMarginPx` / `ToastLaneLeftMarginPx`.

## Notes

- **Retroactive coverage.** This wireframe retrofits the already-shipped `HudOverlay` chip. It keeps the shipped **top-right** anchor, but supersedes the graybox 140×32 `GUI.Box` with the real [CurrencyChip](shared-components.md) and measures the inset from the **safe-area** edges rather than the raw screen edge. The old top banner is being removed ([#207](https://github.com/derekwinters/lucas-doggiehood/issues/207)).
- **Gear co-tenant ([#296](https://github.com/derekwinters/lucas-doggiehood/issues/296), height/alignment [#440](https://github.com/derekwinters/lucas-doggiehood/issues/440)).** The Settings gear ([settings.md](settings.md) decision ①) owns the very corner; the chip is pinned just inboard-left of it (right edge at the gear's left edge minus the gear gap). The chip now **matches the gear's height** and **shares its vertical centreline** — the chip's `y` is derived from the gear's on-screen middle and the chip's own height, so the two read as one clean row. This supersedes the earlier safe-area top-inset (`HudEdgeMarginPx`) positioning: with the chip pinned to the gear (whose rect uses the raw screen edge, unchanged by this pass), the chip is now positioned entirely relative to the gear.
- **Gear rendering ([#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)).** The gear is drawn **procedurally** on IMGUI, matching the chip: hard straight-down Ink drop-shadow, Ink outline disc, cream fill disc, then a procedural Ink **toothed-disc** icon (`GearToothCount` tooth-discs orbiting a cream hub) — via the shared `CandyChrome` primitives, no font glyph and no raster art. It previously used a `⚙` (U+2699) font glyph on a default IMGUI button; the bundled `DejaVuSans` has no coverage for that glyph, so on device it fell back to an empty gray box (the [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291) font/shader-stripping risk). The tap still lands on the same `GearButtonSizePx`/`GearMarginPx` rect (a transparent `GUIStyle.none` hit region) and still opens Settings.
- **Rendering.** The chip stays on IMGUI alongside the gear; its Candy Cottage chrome (stadium pill, Ink outline, hard drop-shadow, gold coin token) is drawn procedurally from a single runtime-generated white circle texture tinted per layer — no external raster art asset. The tabular balance uses the bundled `DejaVuSans` font (never an editor-only built-in), per the [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291) precedent.
- **Coin balance-change animation ([#542](https://github.com/derekwinters/lucas-doggiehood/issues/542)).** On a `Wallet.Coins` change the chip counts its balance up/down to the new total and floats a transient signed delta label (`+N` Leaf / `−N` Coral) just **below** the chip that rises and fades. This is a sub-element and motion of the shared [CurrencyChip](shared-components.md#currency-chip-currencychip) component (owned there, not re-specified here); it is purely decorative and never blocks a tap, so it does not change the chip's anchor or the toast-lane reservation. The tween/rise math is Unity-independent (`Doggiehood.Core.Economy.CoinChipAnimation`); `HudOverlay` only reads and paints it.
- **Toast lane ([#562](https://github.com/derekwinters/lucas-doggiehood/issues/562)).** The **top-left** corner is reserved for the single [toast notification](toast.md) — the same posture as the top-right `CurrencyChip` reservation, in the opposite corner. The lane shares the chip/gear's top margin (`HudToastLaneTopMarginPx` = 32) so both top corners read as one HUD band; it is empty when no toast is showing, and the world/controls never render into it. The toast itself (its regions, dismiss model, and sequential single-slot queue) is specified in the [Toast](toast.md) wireframe; this page only reserves the lane.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md) ([#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
