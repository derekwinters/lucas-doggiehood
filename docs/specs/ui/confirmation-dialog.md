# Confirmation dialog

*Wireframe issue: [#344](https://github.com/derekwinters/lucas-doggiehood/issues/344). Implements/covers: `ConfirmationDialog` (reusable overlay). Approved: <pending>.*
*Mockup: [mockups/confirmation-dialog.html](mockups/confirmation-dialog.html).*

## Purpose

A small, **reusable** centered card over a dimmed scene that asks the player to confirm a single action with **Yes / No**. Both the **title and body text are dynamic** — supplied by whatever opens it — so one dialog serves every "are you sure?" moment rather than a bespoke panel per action. The first consumer is the map-expansion zone unlock ([#343](https://github.com/derekwinters/lucas-doggiehood/issues/343)): tapping the lock icon raises this dialog, and **Yes** calls `GameState.TryUnlockNextZone()`. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Scrim | Full-screen dim behind the card; tapping it **cancels** (equivalent to **No**) so the dialog is never a trap | — |
| Title | The dynamic heading — the question (e.g. *"Unlock this area?"*) | [Shared panel chrome](shared-components.md) |
| Body | The dynamic message — one short sentence of detail (e.g. the cost) | [Shared panel chrome](shared-components.md) |
| Action row | Two equal-width pill buttons: **No** (neutral/cream, left) and **Yes** (positive/leaf, right). When the action costs coins, **Yes** carries a coin token + amount | [PillButton](shared-components.md#pill-button-pillbutton) ×2 |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `DialogAnchor` | `Center` | Card position — centered over a dim scrim so the scene stays visible behind it |
| `DialogWidthPx` | `760` | Card width (narrower than the 900 px profile cards — this is a compact prompt) |
| `DialogPaddingPx` | `48` | Card inset (padding) |
| `TitleFontSizePx` | `44` | Dynamic title |
| `BodyFontSizePx` | `32` | Dynamic body text |
| `TitleBodyGapPx` | `20` | Gap: title → body |
| `ActionRowMarginPx` | `40` | Gap: body → action row |
| `ActionGapPx` | `20` | Gap between the No and Yes buttons |
| `CostCoinDiameterPx` | `40` | Coin token in the Yes button's cost group (when the action has a cost) |
| `CostGapPx` | `8` | Gap between the coin token and the amount |

The panel **chrome** (outline 6 / corner radius 40 / drop-shadow 12–14) and the **No/Yes** buttons (96 px [PillButton](shared-components.md#pill-button-pillbutton), equal width) are owned by the [shared components](shared-components.md) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) — neither is re-specified here. This page introduces **no new atomic component**: it is shared panel chrome wrapping two labels and a row of two shared pill buttons.

## Notes

- **Dynamic text is the whole point.** `Title` and `Body` are strings the caller passes in; nothing about the copy is baked into this layout. The card **grows vertically** with the body length. Messages are meant to be short — one question in the title, one sentence in the body — so the card never needs to scroll; keep copy tight (a caller with a long message should shorten it, not lean on a scrollbar).
- **Buttons.** `No` is the neutral/cream decline on the **left** (the safe default a stray tap lands on); `Yes` is the confirm on the **right**, **always the positive/leaf tint** (not overridable — this dialog stays friendly, never a red/coral "danger" prompt). The labels stay **literally "Yes" / "No"** in every use — the game is for young players, so the actions read as plain words, not verbs a caller rewrites.
- **Cost on Yes.** When the confirmed action spends coins, the **Yes** button shows a **coin token + amount** after the word "Yes" (a `CostCoinDiameterPx` gold coin + a tabular number, `IconGapPx` from the label, `CostGapPx` between coin and number) — so a young player sees the price on the button they're about to press. The amount is passed in by the caller (e.g. the next zone's unlock cost for #343). A **cost-free** confirmation simply shows "Yes" with no coin.
- **Cancel is always reachable.** Tapping the scrim, or **No**, dismisses without acting — the dialog can never soft-lock the player (a deliberate contrast with the onboarding stuck-dialog bug, [#329](https://github.com/derekwinters/lucas-doggiehood/issues/329)). There is no separate ✕ affordance; **No** is the dismiss, keeping the prompt to the two buttons Derek asked for.
- **One instance, reused.** Any screen raises the same overlay by supplying its title, body, optional confirm-tint/labels, and a confirm callback. First use: zone unlock (#343). Natural later reuse: confirming a house build/upgrade spend, or any other "are you sure?" step — each passes its own copy rather than getting a new panel.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type, palette) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
