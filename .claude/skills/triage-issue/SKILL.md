---
name: triage-issue
description: >
  Triage exactly one admitted (`ai-triage`) Doggiehood issue: diagnose a bug,
  plan a spec-covered feature with a proposed milestone, or stop and ask when
  a new design/wireframe decision is needed — never inventing design. Routes
  the single issue to await Derek. Invoked once per issue by the
  `pipeline-analysis` dispatcher, or standalone on one issue number for a
  quick one-off triage.
---

# Triage issue

The **single-issue triage flow** extracted from `pipeline-analysis` (issue
#320) so it is a reusable unit, not welded to a full round. See
`docs/engineering/issue-pipeline.md` and honor the project's hard rules in
`CLAUDE.md` — especially **rule #9 (wireframe before UI)** and the
**no-inventing-design** rule.

## Scope — exactly one issue

**This skill triages exactly one issue** — the issue number it was invoked
with. It:

- **Reads** that issue, its comments (including any `/revise`/`/redo` notes
  or `/propose` from Derek), the `/docs` pages it relates to, and — **as
  needed for context only** — other issues **read-only** (e.g. checking
  whether a candidate blocker is still open, or whether a related issue's
  plan already covers part of this one). It never posts to, labels, or
  otherwise modifies any issue other than the one it was invoked for.
- **Never** moves an issue to `ready-for-work` itself — only the gatekeeper
  does that, on Derek's `/approve`. This skill's own hand-back states are
  `pending-approval` and `needs-clarification` only.

## Invocation

- **Via the dispatcher** — `pipeline-analysis/SKILL.md` runs
  `select_triage.py` to get the eligible issue numbers plus each one's
  context (current milestone + latest owner `/revise`/`/redo`/`/propose`
  note), then invokes this skill once per issue, in parallel with bounded
  concurrency.
- **Standalone** — invoke this skill directly on a single issue number for a
  quick one-off triage (e.g. right after an `/admit`, or to re-triage one
  issue right after a `/revise`) without kicking off a full round. Read the
  issue directly (comments, current milestone, any owner note) instead of
  going through `select_triage.py` — the eligibility rules there (open,
  `ai-triage`, not `type:epic`/the dashboard/`parked`) are a sanity check
  worth applying by eye before triaging an issue by hand.

## The one rule that overrides everything

**Never invent a design decision, mechanic, quest type, breed, or UI layout.**
If a feature needs a call that isn't already settled in `docs/specs/`, or would
touch a UI screen's structure with no approved wireframe in `docs/specs/ui/`,
you **stop and ask** — you do not draft it. The only exception is an issue
carrying an explicit `/propose` from Derek (see below).

## Routing — hybrid by kind

**Invariant: an issue rests in exactly one pipeline state.** The pipeline-state
labels are a mutually-exclusive state machine (`docs/engineering/issue-pipeline.md`
→ "States (labels)"), so **every route below that hands the issue back to Derek
must remove `ai-triage` in the same `issue_write` call that sets the new state
label** (`pending-approval` or `needs-clarification`). There is no deterministic
apply-step behind analysis — you are the only thing that can drop the old label,
so leaving `ai-triage` on re-selects the issue for triage every morning
(`select_triage.py`'s eligibility is just open + `ai-triage`) and leaves the
dashboard's one-slice-per-issue mapping ambiguous (#265, #394).

**Invariant: post the analysis comment FIRST, then set the state label (#582).**
The hand-off is two separate, non-transactional GitHub writes — the analysis
comment (`add_issue_comment`) and the label move (`issue_write` setting the
hand-back state + removing `ai-triage`) — and nothing wraps them in a
transaction, so a session interruption between them leaves a half-hand-off.
**Always do the comment write first and the label write second.** Ordered this
way, the only partial-failure shape possible is *comment-posted-but-label-not-set*
— the label-set-but-comment-missing shape (#569, "looks triaged, no plan for
Derek to `/approve` against") becomes **structurally impossible**, because the
label write can never run before the comment exists. The residual
comment-without-label shape is the safe one: it is caught and requeued/flagged by
the reconcile sweep (`pipeline-reconcile` rules `requeue_triage` /
`flag_orphaned_analysis`, #582) rather than sitting invisibly with a state label
and no plan.

**Re-fire idempotency — repair a partial write, don't repost (#582).** Before
drafting a *new* analysis, check whether this issue is a re-fire landing on a
prior run's partial write: it already carries a triage-authored analysis comment
(the same signature the reconcile sweep recognizes — a `## Build checklist`
heading or a `❓ Needs from Derek/Lucas:` marker) posted **at or after the most
recent re-admission signal** (the latest `ai-triage` add, or the owner's latest
`/revise` / `/redo`), yet **no hand-back state label is set yet**
(`pending-approval` / `needs-clarification`). If so, this is a partial write from
an earlier run that posted the comment but never completed the label move — do
**not** repost a duplicate analysis. **Repair it: apply only the missing label
move** (set the hand-back state you'd have set, remove `ai-triage`, in the one
`issue_write`). The deterministic detection is
`triage-issue/triage_repair.py::is_partial_write_repair(labels, analysis_times,
readmit_time)` (unit-tested in `tests/test_triage_repair.py`), with
`analysis_comment_times(comments)` selecting the analysis comments by signature.
An analysis that *predates* the re-admission is stale (e.g. superseded by a later
`/redo`) and must be re-triaged fresh, not repaired.

Read the issue, its comments (including any `/revise` notes or `/propose`
from Derek), and the `/docs` pages it relates to. Then route:

1. **Bug** → root-cause **diagnosis** + a recommended fix approach, ending with
   a **`## Build checklist`** (acceptance criteria — see below). Add
   `type:bug`. Post the analysis, **set the milestone field** (see milestone
   matching below), and set `pending-approval` **while removing `ai-triage` in
   the same `issue_write` call**.

2. **Feature fully covered by the specs** → a concrete **implementation plan**
   grounded in the relevant `docs/specs/` pages, a matched milestone **set on
   the issue's milestone field** (see milestone matching), and a closing
   **`## Build checklist`** (acceptance criteria — see below). Post it, and set
   `pending-approval` **while removing `ai-triage` in the same `issue_write`
   call**.

3. **Feature needing a new design decision or a UI wireframe (rule #9)** →
   **stop and ask.** Post a clearly-labeled block:

   ```
   ❓ Needs from Derek/Lucas: <one specific, self-contained question,
   stating the options and what each would mean>
   ```

   Set `needs-clarification` **while removing `ai-triage` in the same
   `issue_write` call**. The question must stand on its own — someone reading
   only that block should understand the decision. Never proceed to a plan **or
   a Build checklist** for this kind — it stops at the question. This includes
   an issue that **can't be planned because it's blocked** by an unresolved
   decision in another issue: it rests in `needs-clarification` (**not** bare
   `ai-triage`), so the blocker-revisit sweep re-admits it — `add ai-triage,
   remove needs-clarification` — once its blocker resolves.

   **Don't re-post an unchanged "still blocked" conclusion (#396).** When you
   re-triage an issue that is *already* `needs-clarification` and you reach the
   **same** "still blocked" conclusion on the **same** blocker set as your last
   posted analysis comment (the blocker(s) haven't actually cleared — e.g. a
   wireframe blocker still sits at `ready-for-work`, only approved-to-draft, not
   yet distilled and closed), **do not post a near-duplicate comment.** Leave the
   existing analysis and the `needs-clarification` label in place and hand the
   issue back as a no-op. A revisit that fires while nothing material changed is
   noise; re-posting the identical "still blocked on #N" comment each sweep is
   the visible churn #396 targets. Only post a fresh `needs-clarification`
   analysis when the blocker set or the conclusion has actually changed.

4. **`/propose` present on the issue** (an owner comment containing `/propose`)
   → you are authorized to draft the missing wireframe/mechanic, but only as a
   clearly-marked **PROPOSAL** (prefix the section `PROPOSAL (draft for your
   approval):`), ending with a **`## Build checklist`** (acceptance criteria —
   see below). Set the milestone field (see milestone matching), then set
   `pending-approval` **while removing `ai-triage` in the same `issue_write`
   call**. This is the single opt-in that lets triage suggest design; without
   it, case 3 applies.

When re-triaging after a `/revise`, read Derek's revise notes and address them
directly in the new analysis.

## Build checklist — acceptance criteria on every plan

Every `pending-approval` hand-back (the **Bug**, **spec-covered feature**, and
**`/propose`** routes — *not* `needs-clarification`) ends with a `## Build
checklist`: the acceptance criteria Derek approves and the reviewer checks the
resulting PR against. Without it, `pipeline-dev` invents its own scope and there
is nothing crisp to verify at review.

Write it as **3–8 TDD-ordered checkbox items**, each a single verifiable
criterion in red-green order (failing test → minimum implementation → refactor).
Seed it from the relevant `docs/specs/**` page's own **Build checklist** and
cross-reference that page. Honor the Core/Unity split: any item covering game
logic leads with a **Core** NUnit test before the Unity wiring. Keep each item
checkable — "Core test: building on an empty lot deducts the flat house cost
(named constant)", not "implement house costs".

## Milestone matching — read milestones live, and SET the field (#319)

Match the issue against the **live milestone descriptions** from the
milestones API — never a hard-coded `00`–`08` list, so this survives the
version-numbering rework (#192). Pick the milestone whose description best
fits the work, state which one and why in the analysis, and **set the
issue's milestone field directly** (via `issue_write`) as part of routing it
to `pending-approval` — do not only propose it in the comment's prose. This
is what lets the gatekeeper's `/approve` collapse to a plain presence-check
on the field (issue #319, Part A): the gate no longer scrapes a proposed
milestone out of your analysis comment, so if the field isn't set here,
`/approve` has nothing to check and refuses with a which-milestone hand-back.
Derek's bare `/approve` accepts the milestone you set; `/milestone <name>`
overrides it (its own separate command, unaffected by this rule). See
`docs/engineering/issue-pipeline.md` → "Fetching live milestones" for the
exact recipe (no MCP milestone tool exists; use the JSON API, not the HTML
page, and `issue_write`'s `milestone` parameter takes the milestone's
**number**, not its title).

## Dependencies — always structured, never prose (#248)

**Every dependency you identify MUST be recorded as a structured relationship —
never as prose alone.** The nightly builder (`select_queue.py`) and the
dashboard only see structured forms; a dependency written in a sentence
(e.g. "depends on #109") is invisible to them, so the builder treats a blocked
issue as eligible and can build it before its prerequisite exists (the exact
drift that motivated this rule). Even when the prose already names the issue
number, you must still add the structured line — writing the number in a
sentence is not enough.

Record a dependency **only** one of these ways:

- **Decomposition** (an issue is really several) → create **sub-issues** and
  link them as children.
- **Hard blocker** (issue A can't *start* until issue B merges) → add a
  `Blocked by: #B` line to A's body — one ref per line, keyword then a colon
  then `#N`. Gates eligibility for dev. Use native issue-dependencies too if
  writable.
- **Soft ordering** (A may build, but prerequisite B should sort first) → add a
  `Depends on: #B` line. Orders the build without blocking it.
- **Likely duplicate** → link the candidate issue and note it; don't close
  anything — flag it for Derek.

The colon-bearing line (`Blocked by: #N` / `Depends on: #N`) is the canonical
structured form. The reconciliation sweep flags any body that mentions a
dependency in prose without a matching structured line, so drift is caught — do
not rely on it as a substitute for writing the line correctly the first time.
See `docs/engineering/issue-pipeline.md` → "Recording dependencies" and #197 for
the hard-vs-soft semantics.

**These other-issue reads are the "read-only for context" this skill is
scoped to** — recording a dependency writes only to the **one issue being
triaged** (its own body/labels); it never edits the referenced issue.

## Every hand-back ends with a menu

Close each comment with the context-appropriate "Your move" line:

- `pending-approval` (bug or plan) → `/approve` · `/revise <notes>` · `/redo` ·
  `/park`
- `needs-clarification` → answer inline, or `/revise <notes>` · `/redo` ·
  `/propose` · `/park`

## After triage

Do **not** move the issue to `ready-for-work` yourself — only the gatekeeper
does that on Derek's `/approve`. **Confirm the issue now carries exactly one
pipeline-state label**: the hand-back state you set (`pending-approval` or
`needs-clarification`) and **not** `ai-triage` — you must have removed
`ai-triage` in the same `issue_write` that set the new state. An issue left
carrying both, or left in bare `ai-triage` with no hand-back state, is drift.
Report a one-line summary (number → routed-to state + proposed milestone), and
flag anything you had to stop on.
