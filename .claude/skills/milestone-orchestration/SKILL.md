---
name: milestone-orchestration
description: >
  Serially deliver more than one issue through the doggiehood-dev agent,
  one at a time, all the way to merged. Use whenever Derek asks to delegate,
  build, or work through several issues (two or more) in a milestone in one
  go — e.g. "build #57, #58 and #59", "work through the rest of v0.4",
  "deliver these issues". Each issue gets its own clean branch and PR, waits
  for CI to pass, then merges; the next issue never starts until the previous
  one is merged. Unlike pipeline-dev (nightly, opens PRs and stops), this flow
  drives each issue through merge.
---

# Milestone orchestration — serial delegated delivery

When Derek hands off **more than one issue** to the development agent at once,
this is the flow — always, without being re-instructed each time. It is a thin
serial orchestrator over the existing pieces: the `doggiehood-dev` agent builds
each issue (strict TDD), `ci-watch` waits on each PR, and this skill drives the
branch → PR → CI → **merge** → next handoff in a strict one-at-a-time loop.

See `docs/engineering/issue-pipeline.md` and `docs/engineering/agent-workflow.md`.

## How this differs from `pipeline-dev`

`pipeline-dev` is the **nightly, autonomous** builder: it opens one PR per issue
and **stops** — Derek reviews and merges. This skill is the **Derek-invoked,
interactive** counterpart for an explicit "deliver these issues" handoff: Derek
has already decided, so it drives each issue **through merge** before the next
one begins. The per-issue build (agent, TDD, one-branch/one-PR, `skip-docs`,
`Closes #N`) is identical; the difference is that this skill merges and gates
the next issue on that merge.

## Non-negotiables

1. **One issue at a time, fully.** Finish an issue — merged and its issue
   auto-closed — **before** the next one starts. Never build two in parallel,
   never open the next branch while a prior PR is still open.
2. **Merged is the gate, not "PR opened."** The next issue does not begin until
   the previous PR is **merged** into `main`. An open-but-green PR is not done.
3. **Each issue gets a clean branch off the latest `main`.** Cut every issue's
   branch fresh from `origin/main` **after** the previous issue has merged, so
   each branch already contains all prior delivered work. This is what keeps the
   run conflict-free by construction — never reuse a branch, never stack an
   issue on top of another issue's still-open branch.
4. **CI must be green before merge.** Wait for every check to pass (via
   `ci-watch`) before merging. Never merge a red or still-pending PR.
5. **One issue → one branch → one PR → one squash-merge.** Because each PR
   resolves exactly one issue, its squash-merge lands as one Conventional Commit
   and release-please emits one clean changelog entry. Never batch several issues
   onto a shared branch or PR.
6. **Stop on a build or CI failure — don't skip ahead.** If an issue can't be
   built cleanly, or its CI can't be made green, **halt the whole run** at that
   issue and report. Do **not** merge it and do **not** move on to later issues
   (a later issue may depend on this one; silently reordering breaks the serial
   contract). Leave the state for Derek to inspect.
7. **Strict TDD, Core/Unity split, Conventional Commits** — every issue is built
   by the `doggiehood-dev` agent, which enforces all three. Do not bypass it.

## Resolving the issue set

Derek invokes this by naming the work. Accept either form:

- **An explicit list** — "build #57, #58, #59." Use exactly those, in the given
  order unless a hard blocker forces otherwise (see ordering below).
- **A milestone / queue phrase** — "work through the rest of v0.4." Resolve to
  the open `ready-for-work` issues in that milestone (the same eligibility
  `pipeline-dev` uses: `ready-for-work`, in the milestone, not `parked`, not
  `type:epic`, all hard blockers closed/merged, no open PR).

**Order the resolved set lowest issue number first**, but always place a hard
blocker before anything it blocks (native issue-dependency ∪ any legacy
`Blocked by: #N` line — see `docs/engineering/issue-pipeline.md` → "Recording
dependencies"). If two issues in the set block each other, or a named issue is
blocked by one **outside** the set that isn't yet merged, stop and flag it —
don't guess an order.

Before starting, **echo the resolved, ordered list back to Derek** (one line:
"Delivering, in order: #57 → #58 → #59") so the plan is visible. Then run it.

## Procedure

For each issue in the resolved order, **serially**:

1. **Sync and cut a clean branch.** `git fetch origin main`, then create a fresh
   branch off `origin/main` for this one issue (e.g.
   `pipeline/issue-NN-<short-slug>`). Because prior issues in this run are
   already merged, this branch contains them — no rebase or merge of siblings is
   needed or allowed.

2. **Build the issue with `doggiehood-dev`.** First, **name the approved issue
   to the mechanical issue gate** (CLAUDE.md rule #13): export
   `DOGGIEHOOD_APPROVED_ISSUE=<NN>` for this one issue immediately before the
   agent starts, and clear/replace it when the issue is done. Without it, the
   `PreToolUse` hook (`.claude/hooks/issue_gate.py`) denies every edit to
   `Assets/**`, `CoreTests/**`, `ProjectSettings/**` and `Packages/**`. The
   gate accepts an issue at `ready-for-work` **or** `in-progress`, so it
   passes both before and after the label move below.
   Then hand the single issue number to the
   agent; it works the issue's build checklist test-first, defaults new logic to
   Core, commits with a Conventional Commit message, and reconciles the
   `docs/specs` it touches. Mark the issue `in-progress`.
   - If the agent cannot make it pass (tests red, a docs/spec/wireframe gap it
     must flag, or a genuine blocker): **stop the run** per non-negotiable #6 —
     delete the branch, remove any `in-progress` it added, report which issue
     halted the run and why, and do not touch later issues.

3. **Open exactly one PR** (do not merge yet):
   - **Title** = the issue's single Conventional line (e.g.
     `feat: give approach-to-rest real walk-to-decoration movement`).
   - **Body** starts with the plain-English lead — 2–3 skimmable sentences on
     what was wrong and what changed, before any file/class detail (CLAUDE.md
     rule #12) — then the required `## Deviations and Decisions` section,
     then a dedicated `**Docs:**` line, then `Closes #NN` so the merge
     auto-closes the issue (CLAUDE.md rules #5, #9, #10, #12). Example:

     ```
     Dogs stopped walking to a decoration before resting on it, so they
     teleported into place. They now walk the last stretch on the ground
     network and only settle once they arrive.

     ## Deviations and Decisions

     Deviations: None.
     Decisions: None.

     **Docs:** none needed — no behavior/spec change.

     Closes #185
     ```

   - **If the PR touches no `docs/**` page, apply the `skip-docs` label
     immediately after creating it**, before any other post-open work, so the
     `docs-test` gate's live-label grace poll absorbs the `opened`→`labeled` gap
     (issue #254). A PR that reconciles docs needs no label.

4. **Wait for CI.** Invoke `ci-watch` on the PR and wait for its
   `CI_WATCH_RESULT`.
   - **PASSED** → go to step 5.
   - **FAILED** → apply fixes and push (the standard PR-babysitting flow),
     re-invoke `ci-watch`, and repeat until green. If it genuinely cannot be made
     green, **stop the run** per non-negotiable #6 — leave the PR open for Derek,
     report, and do not proceed to later issues.
   - **TIMEOUT** → investigate runner health and re-invoke; don't merge on a
     timeout.

5. **Merge the PR** with **squash** (one Conventional Commit; matches
   release-please). Confirm the merge landed and the `Closes #NN` keyword
   auto-closed the issue. Only now is this issue done.

6. **Advance.** Return to step 1 for the next issue — the fresh `git fetch` there
   picks up the merge you just made, so the next branch is clean.

After the loop, **report the run**: the issues merged, in order, and — if the run
halted — which issue stopped it and why. Never report a truncated run as a full
delivery.

## Coordination

- Reuses `ci-watch` (CI wait/report) and the `doggiehood-dev` agent (per-issue
  build) — this skill adds only the serial merge-gated loop around them.
- Shares the focus-milestone / eligibility notions with `pipeline-dev`; it does
  **not** read or write the `<!-- pipeline-cap -->` marker — Derek's explicit
  handoff is the scope, not the nightly cap.
- Only Derek invokes this (it merges to `main`). It is not part of any scheduled
  routine.
