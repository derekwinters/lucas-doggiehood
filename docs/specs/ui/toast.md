# Toast notification

*Wireframe issue: [#562](https://github.com/derekwinters/lucas-doggiehood/issues/562). Implements/covers: `ToastNotification`. Approved: Derek, 2026-08-04 ([`/approve` on #562](https://github.com/derekwinters/lucas-doggiehood/issues/562#issuecomment-5185675524)) — authoritative layout contract.*
*Mockup: [mockups/toast.html](mockups/toast.html).*
*Status: **implemented** ([#541](https://github.com/derekwinters/lucas-doggiehood/issues/541)) — the engine-free single-slot `Doggiehood.Core.Ui.ToastQueue<T>` sequences requests; `Assets/Scripts/Unity/ToastView.cs` renders the current slot in the reserved top-left lane (IMGUI HUD element, mirroring the currency chip); and the two directors (`QuestCompletionDirector` off `QuestManager.QuestCompleted`, `OnboardingRewardDirector` off `OnboardingRewardChain.RewardGranted`) assemble copy (`ToastCopy`) and enqueue. Wired in `WorldBootstrap`.*

## Purpose

A small, **non-modal** notification that slides into a reserved **top-left lane** to celebrate a completion the player just earned, then clears itself on a short auto-timeout (or the moment it is tapped). It is the standard "you did that, and here's your reward" surface for two — and only two — triggers: **quest completion** (`QuestManager.Complete`) and **onboarding reward-chain step completion** (`OnboardingRewardChain.RewardGranted`). It is a HUD element like the [currency chip](hud.md), not a centered panel: it never dims the world, never blocks input, and mirrors the chip's reservation in the opposite corner. Reference resolution is 1920×1200 per [Overview](index.md).

Deliberately **not** a centered modal. It replaces the modal-per-step [onboarding reward panel](onboarding-reward.md) for the reward-chain's completion feedback (reversing the #374 "brief modal celebration beat" exception — see [Notes](#notes)) with a lightweight toast that never interrupts play, while still sitting on the shared Candy Cottage baseline and reusing the [`CurrencyChip`](shared-components.md#currency-chip-currencychip) coin token.

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Coin token | A leading gold coin, reusing the [`CurrencyChip`](shared-components.md#currency-chip-currencychip) coin-token treatment — both triggers pay flat coins, so one icon covers both | [CurrencyChip](shared-components.md#currency-chip-currencychip) coin token |
| Message | One short dynamic line: the accomplishment + payout, one sentence. The pill grows to the measured text width up to `ToastMaxWidthPx`; the line **never wraps and never grows height** (`wordWrap` off), and any message that would still exceed the cap is **clipped at the pill edge** (`clipping = Clip`) as a fail-safe rather than bleeding past it. `ToastMaxWidthPx` is sized so every currently-approved line fits on one line at `ToastFontSizePx` ([#578](https://github.com/derekwinters/lucas-doggiehood/issues/578)) | Reuses the Candy Cottage baseline (cream pill, thick outline, hard shadow) from [Shared UI Components](shared-components.md) |
| *(no scrim, no button)* | The toast is the whole surface — no backdrop and no dedicated button; a tap anywhere on it dismisses early | — |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `ToastAnchor` | `TopLeft` | Corner the lane pins to — mirrors [`HudChipAnchor`](hud.md) (`TopRight`) |
| `ToastLaneTopMarginPx` | `32` | Lane top inset — matches the chip/gear row so both corners share one HUD band |
| `ToastLaneLeftMarginPx` | `36` | Lane left inset (safe-area edge) |
| `ToastHeightPx` | `88` | Toast height — matches [`CurrencyChip.HeightPx`](shared-components.md#currency-chip-currencychip) (#173) |
| `ToastMaxWidthPx` | `1080` | Reserved lane width — caps how far a toast reaches toward center screen; widened from 640 ([#578](https://github.com/derekwinters/lucas-doggiehood/issues/578)) so the longest approved line fits on one line at `ToastFontSizePx` |
| `ToastCoinDiameterPx` | `60` | Coin token — matches [`CurrencyChip.CoinDiameterPx`](shared-components.md#currency-chip-currencychip) (#173) |
| `ToastPaddingLeftPx` | `14` | Coin inset — matches [`CurrencyChip`](shared-components.md#currency-chip-currencychip) (#173) |
| `ToastPaddingRightPx` | `28` | Message inset |
| `ToastIconGapPx` | `16` | Coin → message |
| `ToastFontSizePx` | `34` | Message — matches [`MessageFontSizePx`](onboarding-reward.md) (#374) |
| `ToastAutoDismissSeconds` | `3.5` | Auto-timeout before it clears itself |
| `ToastSlideInMs` | `220` | Slide + fade in from the left edge |
| `ToastSlideOutMs` | `180` | Slide + fade out — auto-timeout or tap-dismiss alike |
| `ToastQueueSlotCount` | `1` | One visible toast; the next waits for this one to clear (the settled sequential-queue model) |

The toast's **chrome** (cream pill fill, outline `OutlineThicknessPx` = 6, full pill radius `PillRadiusPx` = 999, hard drop-shadow `ShadowOffsetPx` = 8) and the **coin token** are owned by the [shared components](shared-components.md) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) and not re-specified here. This page adds the toast-specific composition — the reserved lane, the single message line, the dismiss/queue timing — and sizes them.

## Per-toast copy — the two triggers

Both triggers render one line: the accomplishment sentence followed by the flat coin payout as **"+N coins"**. The message is built in the thin Unity layer from Core-supplied data (the just-completed step/quest + amount); no copy lives in Core.

| Trigger | Copy |
|---|---|
| **Quest complete** (any completion path, `QuestManager.Complete`) | *"Quest complete! +N coins"* — N is the flat quest payout ([economy.md](../quests/economy.md), today 10) |
| **Onboarding — first quest** | *"You finished your first quest! +100 coins"* |
| **Onboarding — upgrade a house** | *"You made a house even nicer! +100 coins"* |
| **Onboarding — expand the map** | *"You opened up a brand-new street! +100 coins"* |
| **Onboarding — build a house** | *"You built a brand-new house! +100 coins"* |

The four onboarding lines carry over **verbatim** from the retired [onboarding reward panel](onboarding-reward.md)'s accepted per-step copy (`/approve` [#374](https://github.com/derekwinters/lucas-doggiehood/issues/374)) — only the surface changes, not the words; the panel's separate **"+N coins"** button label folds into the one toast line. The onboarding amount is `OnboardingRewardChainNumbers.RewardPerStep` (100 today); the quest amount is the flat quest payout (10 today). Keep every line to one short sentence that fits on a single line within `ToastMaxWidthPx` at `ToastFontSizePx` — an EditMode guard measures every approved line against the pill's text budget so new copy (or a wider `+N` payout) that would overflow fails loudly instead of clipping the reward amount ([#578](https://github.com/derekwinters/lucas-doggiehood/issues/578)).

## Notes

- **Not modal.** Unlike every centered panel in [shared components](shared-components.md#modal-overlays-block-world-input), the toast never registers with `ModalInputGate` and carries no backdrop — it is a HUD element like the currency chip, just tappable. World taps outside the reserved lane are unaffected; a tap *on* the toast only dismisses it and does not fall through to the world.
- **Dismiss model — both auto-timeout and tap.** The toast clears itself after `ToastAutoDismissSeconds`, and a tap anywhere on it dismisses early; either path runs the same `ToastSlideOutMs` exit. It can never trap the player (the same anti-soft-lock posture as the [confirmation dialog](confirmation-dialog.md) and [#329](https://github.com/derekwinters/lucas-doggiehood/issues/329)).
- **Sequential queue, one slot (`ToastQueueSlotCount` = 1).** Only one toast is ever visible. If a second trigger fires while one is showing — e.g. a quest-complete and an onboarding-reward toast landing close together — the second **waits** and plays immediately after the first's slide-out finishes, never stacked or overlapping. Order is first-come, first-served.
- **Reverses #374's modal exception** (confirmed intended per [#541](https://github.com/derekwinters/lucas-doggiehood/issues/541)'s revise thread): the onboarding reward-chain step's completion feedback moves off the centered `OnboardingRewardPanel` (scrim + button) onto this toast. [onboarding-reward.md](onboarding-reward.md) and [onboarding.md](../onboarding.md#onboarding-reward-chain-316)'s reward-chain section are reconciled accordingly.
- **HUD reservation mirrors the chip.** [hud.md](hud.md) gains a **Toast lane** region, top-left, sized to `ToastHeightPx` + margins — nothing else renders there, the same posture as the [`CurrencyChip`](shared-components.md#currency-chip-currencychip)'s reserved top-right region.
- **Payout stays in Core.** The toast is pure presentation over the existing payout paths (`QuestManager.Complete` → `Wallet.Deposit`, `OnboardingRewardChain` → `Wallet.Deposit`); it shows the coins Core already granted and never moves coins itself. The currency chip updates on its own (it reads `Wallet.Coins` live). A reloaded chain never re-pays nor re-toasts.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type, palette) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
