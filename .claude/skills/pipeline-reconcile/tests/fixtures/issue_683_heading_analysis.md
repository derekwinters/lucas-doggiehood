## Triage — stopping here, this needs a ruling first

**In plain English.** The ask is a small always-visible readout on the HUD counting down to the next batch of quests, so an emptied-out neighborhood reads as "more is coming soon" instead of "the game is out of things to do." The countdown itself is easy — the game already tracks exactly when the next refresh is due, so this is just displaying a number it already knows. What stops it being buildable is that the specs currently say the game has **no timers anywhere**, and that's a design call for you and Lucas, not something implementation gets to reinterpret.

### The tension is real, not hypothetical — I checked the text

`docs/specs/quests/economy.md:19` reads, verbatim:

> - **No pressure**: quests never expire and there is no timer or fail condition anywhere. ([#28](https://github.com/derekwinters/lucas-doggiehood/issues/28))

And `economy.md:12` leans on that same wording to justify how the refresh is built: *"It is a boundary **check**, not a countdown/expiry."* So the spec doesn't merely fail to authorise a countdown — the existing pacing design is explicitly written as "not a countdown" **because** of #28. A visible countdown contradicts the literal text of a standing invariant, and it contradicts the stated rationale for how the system was built.

The issue's own counter-argument is a reasonable reading (a countdown to something *good arriving* is reassurance, not pressure — the same shape as a "next delivery" readout in other kids' games). It may well be the right call. But per the no-inventing-design rule it isn't triage's to make, and per CLAUDE.md rule #12 the fix if you say yes is a **spec amendment**, not an exception quietly carved out in code.

### What's already true (so you know the cost is low once ruled)

The pacing is fully modelled in Core, so this is display-only, not new game logic:

- `QuestPacingPolicy.ShouldRefresh(nowUtc, state)` fires when `EconomyNumbers.RefreshInterval` (1 hour) has elapsed since the persisted UTC `GameState.LastRotationUtc`.
- "Time until next drop" is therefore `LastRotationUtc + RefreshInterval − nowUtc`, clamped at zero — a pure function of already-persisted state, Unity-independent, testable in Core.

### Second gate: this is also a structural HUD change (rule #8)

Independently of the #28 ruling, this adds a **new persistent region** to the HUD. `docs/specs/ui/hud.md:11-17` carries exactly two live regions (the top-right currency chip + gear, and the reserved top-left toast lane) plus a `(reserved) future HUD elements` row whose stated purpose is that *"adding one does not disturb the chip's or the toast lane's anchor."* This is the first taker for that reservation. Per rule #8 that needs an approved wireframe — text spec **and** matching HTML mockup, with named constants — before any implementation code, including graybox. Triage does not draft it.

## ❓ Needs from Derek/Lucas

**Primary (nothing can be built until this is answered): does a visible countdown to the next quest drop violate the #28 "no pressure" invariant, or not?**

- **Option A — it's allowed.** Countdowns to something arriving are reassurance, not pressure. #28's intent is that nothing the player *holds* ever expires or fails. Consequence: `economy.md:19` is amended to read as *"nothing the player holds ever expires or fails"* rather than a blanket ban on visible timers, with a `> **How the spec is changing (#683).**` note; `QuestPacingPolicy`'s "not a countdown" doc comment is corrected too. Then this becomes buildable, gated on the wireframe below.
- **Option B — it stands as written.** No visible timers anywhere, full stop. Consequence: this issue closes as *won't do*, and the "neighborhood looks empty" problem gets solved a different way (e.g. a static "more dogs will need help soon" line, or nothing).

**If Option A, three follow-ons that must be settled at the same time or the readout will be wrong:**

1. **What does it say when a drop adds nothing?** A refresh at the concurrent cap, with no free dogs, or with the fractional accumulator not yet tipped over a whole quest, adds **zero** quests — and that is normal, not an edge case. A countdown that promises a quest and delivers nothing is worse than no countdown. Options: hide it at `activeCount >= TargetActiveCount`; swap to a "neighborhood's full" state; or word it *"next check"* rather than *"next quest."*
2. **Granularity and format** — live `MM:SS`, a coarse `~12m`, or a filling ring/pill with no numerals? This isn't cosmetic: a ticking second counter is exactly the urgency #28 was written to keep out, so it partly re-opens the primary question.
3. **Visible during onboarding?** The rotation is suppressed across the whole onboarding reward-chain (#312/#316/#579), so there is no meaningful next drop until it completes. Presumed hidden — confirm.

**And a routing call:** should the HUD wireframe be its own `type:wireframe` issue (this one then blocked by it), or folded into this issue as its first deliverable the way #690 folds in the confirmation-dialog wireframe? Either is fine; folding it in keeps one thread, splitting it lets the wireframe be approved independently.

**Your move:** answer inline, or `/revise <notes>` · `/redo` · `/propose` · `/park`

*(`/propose` would authorise a drafted wireframe **and** a proposed `economy.md` amendment as a clearly-marked PROPOSAL for your approval — useful if you'd rather react to something concrete than answer in the abstract. It does not decide the #28 question.)*

---
_Generated by [Claude Code](https://claude.ai/code)_