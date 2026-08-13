# Confirmation dialog

*Wireframe issue: [#344](https://github.com/derekwinters/lucas-doggiehood/issues/344). Implements/covers: `ConfirmationDialog` (reusable overlay). Approved: Derek, 2026-07-28 (in-session).*
*Mockup: [mockups/confirmation-dialog.html](mockups/confirmation-dialog.html).*

## Purpose

A small, **reusable** centered card over a dimmed scene that asks the player to confirm a single action with **Yes / No**. Both the **title and body text are dynamic** — supplied by whatever opens it — so one dialog serves every "are you sure?" moment rather than a bespoke panel per action. The first consumer is the map-expansion zone unlock ([#343](https://github.com/derekwinters/lucas-doggiehood/issues/343)): tapping the lock icon raises this dialog, and **Yes** calls `GameState.TryUnlockNextZone()`. Reference resolution is 1920×1200 per [Overview](index.md).

> **How the spec is changing ([#690](https://github.com/derekwinters/lucas-doggiehood/issues/690)).** This page used to describe only one **Yes**: leaf-tinted and pressable, with a coin token when the action costs coins → it now also describes a **disabled Yes**, greyed to the shared Disabled tint and non-interactable, for a spend the wallet can't cover → because a player who tapped a foundation slab they couldn't afford got a normal-looking **Yes** that silently did nothing, which for a young player is indistinguishable from a broken button. No layout constant, anchor, or region moves; this is a new *state* of the existing Action row, using the Disabled role that [shared components](shared-components.md) already locks.

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Scrim | Full-screen dim behind the card; tapping it **cancels** (equivalent to **No**) so the dialog is never a trap. Being a full-screen raycast target, it also makes the dialog **modal** — a tap anywhere over it is absorbed and never reaches a world interactable behind the card ([#422](https://github.com/derekwinters/lucas-doggiehood/issues/422)) | — |
| Title | The dynamic heading — the question (e.g. *"Unlock this area?"*) | [Shared panel chrome](shared-components.md) |
| Body | The dynamic message — one short sentence of detail (e.g. the cost) | [Shared panel chrome](shared-components.md) |
| Action row | Two equal-width pill buttons: **No** (neutral/cream, left) and **Yes** (positive/leaf, right). When the action costs coins, **Yes** carries a coin token + amount. When the caller says the action **can't succeed** (an unaffordable spend), **Yes** renders in the shared **Disabled** role tint and is not pressable — the cost token stays visible | [PillButton](shared-components.md#pill-button-pillbutton) ×2 |

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
- **A Yes the player can't afford is shown greyed out, never pressable** ([#690](https://github.com/derekwinters/lucas-doggiehood/issues/690)). A caller that spends coins tells the dialog whether the spend can actually succeed; when it can't, **Yes** takes the shared **Disabled** role tint (grey `#D8D2C6` fill, [shared components](shared-components.md#shared-palette)) and is **non-interactable** — realized the same way every other disabled control in the game is: an unaffordable quest gift pill ([#186](https://github.com/derekwinters/lucas-doggiehood/issues/186)) and the unaffordable house-upgrade button ([#294](https://github.com/derekwinters/lucas-doggiehood/issues/294)). The dialog still **opens** and the **cost token stays on Yes**, so a young player sees the price they are short of rather than tapping a button that does nothing. Affordability is **resolved once, when the dialog opens** — matching the quest pills, which are built once when their panel opens; a payout arriving mid-prompt does not re-enable the button, the next tap opens a live one. Nothing about the layout changes: same constants, same two buttons, same coin token.

  > **Invariant — a spend button is never pressable when the spend cannot succeed.** Any control that charges coins resolves its affordability from Core *before* it is presented, and renders itself disabled when the wallet can't cover it. A player is never offered a button whose only outcome is nothing happening.

  A disabled **Yes** is **presentation only**: Core stays the sole authority on the spend, so the confirm path must still reject an unaffordable action if it is ever reached.

- **Cancel is always reachable.** Tapping the scrim, or **No**, dismisses without acting — the dialog can never soft-lock the player (a deliberate contrast with the onboarding stuck-dialog bug, [#329](https://github.com/derekwinters/lucas-doggiehood/issues/329)). This holds **while Yes is disabled** too: greying the confirm never greys the way out. There is no separate ✕ affordance; **No** is the dismiss, keeping the prompt to the two buttons Derek asked for.
- **One instance, reused.** Any screen raises the same overlay by supplying its title, body, optional confirm-tint/labels, and a confirm callback. Live consumers: the zone unlock (#343, first use — *"Unlock this area?"*) and the **house build** ([#406](https://github.com/derekwinters/lucas-doggiehood/issues/406) — *"Build a house here?"* + the flat 50-coin cost on Yes; see [expansion.md](../expansion.md#the-loop)). Both are friendly spends keeping the literal Yes/No labels and leaf confirm tint. Natural later reuse: any other "are you sure?" step — each passes its own copy rather than getting a new panel. (House *upgrade* is intentionally **not** a consumer — it spends directly on tap, Derek's Option A, [#294](https://github.com/derekwinters/lucas-doggiehood/issues/294).)
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type, palette) lives in [Art & UI Style](../world/art-style.md); this page is layout only.

**Implementation note ([#343](https://github.com/derekwinters/lucas-doggiehood/issues/343)).** Built as `Doggiehood.Unity.ConfirmationDialog` — a retained-UGUI overlay under the shared [#256](https://github.com/derekwinters/lucas-doggiehood/issues/256) `UiCanvas`, with every layout value above a named constant asserted by EditMode tests (#161). `Open(title, body, onConfirm, cost?, …)` sets the dynamic title/body, shows the coin token + amount on **Yes** when `cost` is non-null, and runs the caller's callback on confirm; **No** and the scrim both cancel (dismiss without acting). Its `confirmEnabled` argument (**[#690](https://github.com/derekwinters/lucas-doggiehood/issues/690)**, defaulting to `true` so a caller with nothing to gate is unchanged) is what greys **Yes**: `false` paints the button `CandyChromeUgui.Disabled` and clears `Button.interactable`, leaving the cost token in place. Callers pass an affordability answer resolved in **Core** — the house build passes `HouseBuildOffer.IsAffordable` — never a Unity-side wallet comparison. Labels default to literal **Yes/No** and the confirm tint to **leaf**, but both accept an override so the one overlay is genuinely reusable by later spend confirmations. Chrome is the device-safe shared `CandyChromeUgui` (#298) — rounded panel + pill buttons through the always-included `UI/Default` material, no custom shader — and text uses the bundled font loaded via `Resources` (never an editor-only builtin), the two #291 patterns the merged overlays follow. The card grows vertically with the body's wrapped height. First consumer: the map-expansion zone unlock (see [expansion.md](../expansion.md#expansion-indicator-discoverability)).
