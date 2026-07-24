# Conversation panel

*Wireframe issue: [#175](https://github.com/derekwinters/lucas-doggiehood/issues/175). Implements/covers: `ConversationPresenter`. Approved: Derek, 2026-07-24 (in-session).*
*Mockup: [mockups/conversation-panel.html](mockups/conversation-panel.html).*

## Purpose

Opens when a dog's speech bubble is tapped: the dog states its request, and the player either accepts it or declines. It is the sole surface for taking on (or handing in) a quest — the speech bubble is the only quest-discovery mechanism (see [Conversation System](../quests/conversation-system.md)). Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Name tag | The talking dog's name in an overlapping tab at the top-left of the panel | [DialogueBox](shared-components.md#dialogue-box-dialoguebox) |
| Body | The dog's request as linear text | [DialogueBox](shared-components.md#dialogue-box-dialoguebox) |
| Action row | Right-aligned action buttons — the accept/complete affordance plus a "Not now" decline. The row flexes by quest type (see [Notes](#notes)) | [PillButton](shared-components.md#pill-button-pillbutton) |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `PanelAnchor` | `BottomCenter` | Panel position — sits bottom-center so the dog and neighborhood stay visible above it |
| `PanelWidthPx` | `1040` | Panel width (centered) |
| `PanelBottomMarginPx` | `64` | Gap below the panel |
| `BodyFontPx` | `34` | Request text size |

The panel **chrome** (padding 40 / corner radius 40 / drop-shadow 12 / name-tag overlap 28 / action-row gap 20) is owned by the shared [DialogueBox](shared-components.md#dialogue-box-dialoguebox) shell ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)), and the action **buttons** (96 px pills) by [PillButton](shared-components.md#pill-button-pillbutton) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) — neither is re-specified here; this page only places those components and sizes the panel and body text.

## Notes

- **Action-row variants — the region flexes by quest type.** The decline ("Not now") is always present; the accept affordance changes:
    - **Standard quest** — an `Accept` pill (or `Complete` when returning to hand the quest in), plus **Not now**.
    - **Decoration / choice quest** — one option pill per choice (the existing `AcceptChoice` behavior), each showing its catalog cost with the same `Name · Cost` convention as the buy pill (e.g. `Cushion · 30`) and greying out when unaffordable, plus **Not now**.
    - **Buy-something quest** — the accept pill shows the cost (e.g. `Buy · 40`) and greys out when unaffordable.
- **Insufficient-funds handling ([#186](https://github.com/derekwinters/lucas-doggiehood/issues/186)).** Greying out an unaffordable pill is the proactive signal, but accepting is also checked again on the actual attempt (`QuestManager.Accept`/`AcceptWithChoice`, per [Quest & Economy](../quests/economy.md)); if that spend is rejected, the panel stays **open** and shows a short insufficient-funds message instead of closing silently — a failed purchase must never look like a no-op.
- **Decline behavior.** **"Not now"** dismisses the panel without accepting. It is **non-punishing** — no timers or fail states are started (per [Quest & Economy](../quests/economy.md)) — and **re-openable**: the dog keeps its speech bubble, so the player can tap it again later. **Tapping outside the panel** is the same as "Not now". A decline is an exit, not a dialogue branch — there is still no branching dialogue tree (see [Conversation System](../quests/conversation-system.md)).
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
