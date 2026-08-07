# Welcome pop-up

*Wireframe issue: [#439](https://github.com/derekwinters/lucas-doggiehood/issues/439). Implements/covers: `WelcomePopup`. Approved: Derek, 2026-07-31 ([proposal](https://github.com/derekwinters/lucas-doggiehood/issues/439#issuecomment-5147513906) + [decisions](https://github.com/derekwinters/lucas-doggiehood/issues/439#issuecomment-5147589722), [`/approve` on #439](https://github.com/derekwinters/lucas-doggiehood/issues/439#issuecomment-5148148003)) — authoritative layout contract.*
*Amended: the **Close (✕)** region and `CloseButtonSizePx` were added by [#671](https://github.com/derekwinters/lucas-doggiehood/issues/671), drafted under `/propose` and approved by Derek, 2026-08-07 — part of the authoritative contract.*
*Mockup: [mockups/welcome-popup.html](mockups/welcome-popup.html).*

## Purpose

A **modal celebration panel** — *"Welcome to the neighborhood!"* — raised when the [move-in system](../expansion.md#move-in-system) fills a vacant house, announcing the new arrival(s) to the player. It **reuses the [onboarding reward panel](onboarding-reward.md) composition** (portrait medal overlapping the top edge, big heading, one leaf pill), just parameterized for an arrival instead of a coin payout, so a move-in reads as the same warm celebration beat. One pop-up per household. Its single **"Say hi!"** button pans the camera to the new house **and opens that house's [profile](house-profile.md)** ([#604](https://github.com/derekwinters/lucas-doggiehood/issues/604)) so the player lands on their new neighbour with the resident dog(s) one tap away. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Scrim | Full-screen dim behind the panel; tapping it dismisses **without panning** so the celebration is never a trap | — |
| Close | Top-right ✕ dismiss affordance, flush in the card's corner. Dismisses **without** panning the camera and **without** opening the house profile — the visible counterpart to the scrim tap ([#671](https://github.com/derekwinters/lucas-doggiehood/issues/671)) | [Shared panel chrome](shared-components.md) |
| Portrait badge | A single round medal overlapping the panel's top edge, carrying the new dog's (household head's) portrait — a **graybox dog silhouette now**; a real tinted dog-model portrait is a fast-follow (see Notes) | Reuses the Candy Cottage baseline (thick outline, hard shadow) from [Shared UI Components](shared-components.md) |
| Heading | The fixed celebratory headline — *"Welcome to the neighborhood!"* | [Shared panel chrome](shared-components.md) |
| Name | The new dog's name, or the household's names for a multi-dog move-in (e.g. *"Biscuit & Pepper"*) | [Shared panel chrome](shared-components.md) |
| Meta | One dynamic line: breed · household composition / which house (e.g. *"French Bulldog · moved in next door"*, *"French Bulldog family of 2"*, *"moved in — 3 dogs"*) | [Shared panel chrome](shared-components.md) |
| Member-chip row | A row of small named portrait chips, one per dog in the household — the household head keeps the big portrait badge. **Hidden entirely for a single-dog move-in.** | Reuses the [chip shape](shared-components.md) baseline |
| Action | One positive/leaf pill button — **"Say hi!"** — which dismisses, pans the camera to the new house, **and** opens that house's [profile](house-profile.md) ([#604](https://github.com/derekwinters/lucas-doggiehood/issues/604)) | [PillButton](shared-components.md#pill-button-pillbutton) |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `WelcomeAnchor` | `Center` | Panel position — centered over a dim scrim so the neighborhood stays visible behind it |
| `WelcomeWidthPx` | `820` | Panel width (matches the reward panel — this is a hero moment) |
| `WelcomePaddingPx` | `56` | Panel inset (padding) |
| `PortraitDiameterPx` | `176` | Round portrait medal badge |
| `PortraitOverlapPx` | `88` | How far the portrait medal rises above the panel's top edge |
| `PortraitOutlineThicknessPx` | `8` | Portrait medal's ink ring |
| `PortraitTopGapPx` | `28` | Portrait medal's lower half → heading |
| `HeadingFontSizePx` | `54` | Fixed "Welcome to the neighborhood!" headline |
| `NameFontSizePx` | `40` | Dog name / household names line |
| `MetaFontSizePx` | `30` | Breed · household / which-house line |
| `HeadingNameGapPx` | `18` | Heading → name |
| `NameMetaGapPx` | `8` | Name → meta |
| `MetaActionMarginPx` | `40` | Meta (or member-chip row) → action button |
| `ActionMinWidthPx` | `320` | The single **Say hi!** pill (centered; grows with the label) |
| `CloseButtonSizePx` | `72` | Close (✕) button, flush top-right — matches [dog profile](dog-profile.md) and [house profile](house-profile.md) ([#671](https://github.com/derekwinters/lucas-doggiehood/issues/671)) |
| `WelcomePopupDelaySeconds` | `1.5` (range 1–3) | Beat after the prior panel closes before this pops (see [Timing](#timing)) |
| `MemberChipDiameterPx` | `72` | Each per-dog portrait chip in the member row (multi-dog only) |
| `MemberChipGapPx` | `20` | Gap between member chips |
| `MemberRowMarginPx` | `28` | Meta line → member-chip row |

The panel **chrome** (outline `OutlineThicknessPx` = 6 / corner radius `PanelRadiusPx` = 40 / hard drop-shadow) and the **Say hi!** button (96 px [PillButton](shared-components.md#pill-button-pillbutton)) are owned by the [shared components](shared-components.md) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) and not re-specified here — the same reference posture the [onboarding reward panel](onboarding-reward.md) takes. This page adds the welcome-specific composition — the portrait medal, the heading, the name, the meta, the member-chip row, and the button — and sizes them.

## Household variants

The move-in system produces households in a **70% single / 25% parent+puppy / 5% three-dog** mix ([Move-in system](../expansion.md#move-in-system)). There is **one pop-up per household**, never one per dog; the household head keeps the big portrait badge.

- **Single (70%)** — the common case: one portrait badge, one name, one meta line. The **member-chip row is hidden** — a lone dog needs no chip echoing the portrait above it.
- **Parent + puppy (25%)** — the household head's portrait in the badge; the name line reads both names (e.g. *"Biscuit & Pepper"*); the meta reads the shared breed and family size (e.g. *"French Bulldog family of 2"*). A **member-chip row of two named chips** appears below the meta, one per dog.
- **Three-dog (5%)** — one pop-up for the whole household (not three), head's portrait in the badge, meta *"moved in — 3 dogs"*, and a **member-chip row of three named chips**.

The **hidden-for-single rule**: the member-chip row renders only when the household has more than one dog. For single move-ins the panel collapses to portrait → heading → name → meta → button, with no empty row and no reserved gap.

## Timing

The panel pops **`WelcomePopupDelaySeconds` (range 1–3s, default 1.5s) *after* the previous panel closes** — the move-in is triggered by a completed quest, and this delay keeps the welcome from stacking on top of the quest-resolution feedback that fired it. The move-in celebration is always its own clean beat, never overlaid on another panel. `WelcomePopupDelaySeconds` is a named, tunable constant like every value here.

## Notes

- **One pop-up per household, parameterized.** The caller passes the household (head + members), their names, breed, and which house; the heading and chrome are constant. The card grows vertically with its content (name/meta wrap, member-chip row present or absent), exactly like the [reward panel](onboarding-reward.md) and [confirmation card](confirmation-dialog.md).
- **Always dismissible, and visibly so.** Three paths close the panel: the top-right **✕** and a **scrim tap** both dismiss *without* panning or opening anything, and **Say hi!** dismisses *and* pans *and* opens the profile. The ✕ is what makes the anti-soft-lock guarantee ([#329](https://github.com/derekwinters/lucas-doggiehood/issues/329)) **discoverable** rather than merely true: before it, the scrim tap was the only way to decline, and nothing signalled it, so the sole visible control committed the player to a camera move and a profile panel ([#671](https://github.com/derekwinters/lucas-doggiehood/issues/671)). One *action* button only — a welcome is an acknowledgement, not a choice — but declining an acknowledgement must be visible.

  **Why a ✕ here and not on the [confirmation dialog](confirmation-dialog.md):** the ✕ is this project's convention for panels you *close*, and is deliberately absent only where another control already means *cancel*. The confirmation dialog's **No** is that control; **Say hi!** is an accept, not a dismiss, so this panel has none — which is exactly what puts it in the first group alongside the [dog](dog-profile.md) and [house](house-profile.md) profiles.

- **The ✕ clears the portrait medal by 250 px.** The medal is centered and `PortraitDiameterPx` wide, so at the reference resolution it spans x 872–1048 within the `WelcomeWidthPx` = 820 card (left edge 550); the ✕ sits flush at the card's top-right, spanning x 1298–1370. They overlap vertically — the medal's lower half and the ✕ share the card's top band — but never horizontally. The medal is centered regardless of content, so this clearance is **constant across all three household variants** and does not need re-checking per variant.
- **The camera pan and profile-open are the non-presentational behaviors.** Everything else on this panel is pure presentation, but **"Say hi!" pans the camera to the new house** via `CameraController.FocusOn` **and opens that house's [profile](house-profile.md)** ([#604](https://github.com/derekwinters/lucas-doggiehood/issues/604)) — so tapping it is truthful: the player is taken to *meet* their new neighbour, not just look at their roof, with the resident dog(s) one tap away. The profile-open reuses the exact same resolve a house tap uses (`WorldBootstrap.OpenHouseProfile` → `HouseProfileOverlay.Open`), so there is no duplicated lookup. The pan reuses the existing `FocusOn` and is a slightly wider scope than [#373](https://github.com/derekwinters/lucas-doggiehood/issues/373) chose for zone-unlock (which deliberately only let the player *pan* to a new zone). The welcome unregisters from the modal input gate on dismiss and the profile registers on open within the same synchronous **Say hi!** call, so exactly one modal stays registered across the hand-off and no world tap leaks in between ([#544](https://github.com/derekwinters/lucas-doggiehood/issues/544)). Tapping the scrim dismisses without panning **or** opening anything.
- **Portrait is graybox now.** The portrait badge and member chips show a graybox dog silhouette for now; a real tinted **dog-model portrait** — needs no new art, it renders the existing coat-tinted model — is a fast-follow, not part of this wireframe.
- **A brief celebration beat.** Like the [reward panel](onboarding-reward.md), this is a momentary modal (scrim + one button) the player taps through — a warm earned beat, not a tutorial screen. Move-ins are paced (gated behind a built vacant house), so a big earned beat fits.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type, palette) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
