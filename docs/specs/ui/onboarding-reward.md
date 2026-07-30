# Onboarding reward panel

*Wireframe issue: [#374](https://github.com/derekwinters/lucas-doggiehood/issues/374). Implements/covers: `OnboardingRewardPanel`. Approved: Derek, 2026-07-30 ([`/approve` on #374](https://github.com/derekwinters/lucas-doggiehood/issues/374#issuecomment-5125178403)) — authoritative layout contract.*
*Mockup: [mockups/onboarding-reward.html](mockups/onboarding-reward.html).*

## Purpose

A single, **reusable celebration panel** shown each time the player completes a step of the first-run onboarding reward-chain ([#316](https://github.com/derekwinters/lucas-doggiehood/issues/316)) — finish the first quest, upgrade a house, expand the map, build a house. It tells a young player, unmistakably, *"you did that, and here's your reward,"* calling out the coin payout that today lands silently. It is the **standard** onboarding reward surface: one panel, parameterized by copy + amount, raised after every onboarding step rather than a bespoke dialog per flow ([#374](https://github.com/derekwinters/lucas-doggiehood/issues/374); consumer [#372](https://github.com/derekwinters/lucas-doggiehood/issues/372)). Reference resolution is 1920×1200 per [Overview](index.md).

Deliberately a **bespoke celebration panel**, not a reuse of the neutral [Confirmation dialog](confirmation-dialog.md) (Derek, 2026-07-29): it reads as a *reward* — a single big star medal, a big "You did it!" heading, and one button that **is** the payout (**"+100 coins"**) — while still sitting on the shared Candy Cottage baseline and reusing the shared pill button.

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Scrim | Full-screen dim behind the panel; tapping it dismisses (same as the button) so the celebration is never a trap | — |
| Medal badge | A single round gold medal overlapping the panel's top edge, carrying one big ink star — the "you earned it" marker | Reuses the Candy Cottage baseline (thick outline, hard shadow) from [Shared UI Components](shared-components.md) |
| Heading | The fixed celebratory headline — *"You did it!"* | [Shared panel chrome](shared-components.md) |
| Message | One short dynamic line naming what the player just did (e.g. *"You finished your first quest!"*) | [Shared panel chrome](shared-components.md) |
| Action | One positive/leaf pill button that names the payout — **"+100 coins"** with a gold coin token — and dismisses | [PillButton](shared-components.md#pill-button-pillbutton) |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `RewardAnchor` | `Center` | Panel position — centered over a dim scrim so the neighborhood stays visible behind it |
| `RewardWidthPx` | `820` | Panel width (wider than the 760 px confirmation card — this is a hero moment) |
| `RewardPaddingPx` | `56` | Panel inset (padding) |
| `MedalDiameterPx` | `176` | Round medal badge |
| `MedalOverlapPx` | `88` | How far the medal rises above the panel's top edge |
| `MedalOutlineThicknessPx` | `8` | Medal's ink ring |
| `MedalTopGapPx` | `28` | Medal's lower half → heading |
| `HeadingFontSizePx` | `60` | Fixed "You did it!" headline |
| `MessageFontSizePx` | `34` | Dynamic accomplishment line |
| `HeadingMessageGapPx` | `16` | Heading → message |
| `MessageActionMarginPx` | `44` | Message → action button |
| `ActionMinWidthPx` | `320` | The single **+100 coins** pill (centered; grows with the label) |
| `ButtonCoinDiameterPx` | `56` | Gold coin token inside the button |
| `ButtonCoinGapPx` | `18` | Coin token → `+N coins` label |

The panel **chrome** (outline `OutlineThicknessPx` = 6 / corner radius `PanelRadiusPx` = 40 / hard drop-shadow) and the **+100 coins** button (96 px [PillButton](shared-components.md#pill-button-pillbutton)) are owned by the [shared components](shared-components.md) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) and not re-specified here. This page adds the celebration-specific composition — the medal, the heading, the message, and the payout button — and sizes them.

## Notes

- **One instance, reused for every onboarding step.** The caller passes the **message** (what was done) and the **amount** (`+100` today — `OnboardingRewardChainNumbers.RewardPerStep`), which renders as the button label (**"+100 coins"**); the heading and chrome are constant. This is the standard onboarding reward surface — no per-step panel ([#374](https://github.com/derekwinters/lucas-doggiehood/issues/374)).
- **Dynamic message, fixed heading.** Only the one message line is dynamic; the card **grows vertically** with its wrapped height, exactly like the [confirmation card](confirmation-dialog.md). Keep each message to one short sentence — no scrolling.
- **Per-step copy** (message line — accepted defaults, `/approve` #374):
    1. **First quest** — "You finished your first quest!"
    2. **Upgrade a house** — "You made a house even nicer!"
    3. **Expand the map** — "You opened up a brand-new street!"
    4. **Build a house** — "You built a brand-new house!"
- **Always dismissible.** The single **+100 coins** button dismisses; tapping the scrim also dismisses. There is no path that leaves it stuck open — the same anti-soft-lock posture as [#329](https://github.com/derekwinters/lucas-doggiehood/issues/329) and the [confirmation dialog](confirmation-dialog.md). One button only — a reward is an acknowledgement, not a choice, so there is no decline; the single button names the payout rather than reading a neutral "Yay!".
- **A brief celebration beat, not tutorial guidance.** Unlike the non-blocking [onboarding coach prompt](onboarding-overlay.md), this panel *is* a momentary modal (scrim + one button) the player taps through after finishing a step. **Accepted (Derek, `/approve` #374):** a brief one-tap modal celebration is the standard here — a deliberate, bounded exception to onboarding's otherwise "no blocking modal" principle ([onboarding.md](../onboarding.md)). It is a reward beat, not a tutorial screen; the panel is always dismissed by the single button (or the scrim).
- **Reward payout stays in Core.** The panel is pure presentation over the existing reward-chain deposit (`OnboardingRewardChain` / `Wallet.Deposit`); it shows the amount Core already granted and never moves coins itself. The currency chip updates on its own (it reads `Wallet.Coins` live).
- **Scope (accepted, Derek `/approve` #374):** onboarding-only for now. Reusing this same panel as the reward feedback for **every** completed quest is a natural follow-up but stays out of scope until Derek/Lucas decide it separately — see [#372](https://github.com/derekwinters/lucas-doggiehood/issues/372).
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type, palette) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
