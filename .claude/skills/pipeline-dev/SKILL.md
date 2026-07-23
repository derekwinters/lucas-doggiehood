---
name: pipeline-dev
description: >
  Nightly serial builder for the Doggiehood issue pipeline. Selects the
  eligible ready-for-work issues in the focus milestone, builds each with the
  doggiehood-dev agent (strict TDD) on its own branch, and opens one PR per
  issue. Never merges, never closes. Use in the nightly development routine,
  after the gatekeeper has run.
---

# Pipeline dev — nightly serial builder

Wraps the existing `doggiehood-dev` agent with queue selection, a serial build
loop, and per-issue PR assembly. Runs in the **nightly** routine (1 AM CT),
after the gatekeeper. See `docs/engineering/issue-pipeline.md`.

## Non-negotiables

1. **Never merges, never closes.** This skill opens one PR per built issue and
   stops. Derek reviews and merges; PR-babysitting keeps each green.
2. **Strict TDD per issue** — every issue is built by the `doggiehood-dev`
   agent, which enforces red-green-refactor. Do not bypass it.
3. **One issue → one branch → one PR.** Each issue is built on its own branch
   off `main` and opened as its own PR, so its squash-merge lands as exactly
   one Conventional Commit and release-please emits one clean changelog entry.
   If an issue fails to build cleanly, drop it entirely (delete its branch, no
   PR) and continue to the next.
4. **Respect the focus milestone and the nightly cap** (3 to start).

## Focus milestone

The active milestone is read from the `<!-- pipeline-focus: ... -->` marker on
the dashboard issue (#193), written by the gatekeeper on `/focus`. If the
marker is absent, default to the lowest-numbered milestone that has open
`ready-for-work` issues.

## Procedure

1. **Read focus** from the #193 marker (or default as above).

2. **Gather the snapshot** with the GitHub MCP tools: every open issue that has
   `ready-for-work`, with its `labels`, `milestone`, `is_epic`, whether it has
   an open PR (`has_open_pr`), its hard blockers (`blocked_by` — issues it
   can't start until they close/merge, from sub-issue parents and
   `Blocked by: #N` lines), and its ordering hints (`depends_on` — sibling
   sub-issues or soft prerequisites). Also collect `open_issue_numbers` (all
   currently-open issue numbers) so blockers can be resolved.

3. **Select the queue** deterministically:

   ```bash
   python3 .claude/skills/pipeline-dev/select_queue.py < snapshot.json
   ```

   Returns `build_order` (eligible set, topologically ordered), `selected`
   (truncated to the cap), `capped_out`, and `skipped` (with reasons). Build
   only `selected`, in order.

4. **Serial per-issue build loop** — for each issue in `selected`, in order:
   - **Create a fresh branch** for that one issue off the latest `main`
     (e.g. `pipeline/issue-NN-YYYYMMDD`). One issue per branch — never batch
     several issues onto a shared branch.
   - Run the `doggiehood-dev` agent on that single issue, committing onto its
     branch with a Conventional Commit message. Mark the issue `in-progress`.
   - If the agent cannot make it pass (tests red, blocked, or it flags a
     docs/spec gap): **drop the issue entirely** — delete its branch and open
     no PR — remove any `in-progress` it added back to `ready-for-work`, and
     record the reason. Continue to the next issue.

5. **Open one PR for that issue** (never merge), before moving to the next:
   - **Title** = that issue's single Conventional line, e.g.
     `feat: give approach-to-rest real walk-to-decoration movement`. Because
     the PR resolves exactly one issue, its squash-merge lands as one
     Conventional Commit and release-please emits one clean changelog entry —
     no raw-lines-in-body trick is needed or allowed. See
     `docs/engineering/versioning.md`.
   - **Body** starts with the required `## Deviations and Decisions` section
     (per `docs/engineering/agent-workflow.md`), followed by a `Closes #NN`
     line so merging auto-closes the issue. Example body:

     ```
     ## Deviations and Decisions

     Deviations: None.
     Decisions: None.

     Closes #185
     ```

6. **After the loop, report the run**: list any dropped issues and why, and any
   `capped_out` issues deferred to the next night. **Log the cap explicitly** so
   a truncated queue never reads as "everything was built."

7. **Babysit** each opened PR to keep CI green (the standard PR-activity flow);
   do not merge or close.

## Coordination

- `/focus` storage is shared with `pipeline-gatekeeper` (the #193 marker).
- The nightly cap starts at **3**; change it in one place — the `cap` passed to
  `select_queue.py` in this procedure.

## Tests

`tests/test_select_queue.py` covers eligibility (label, milestone, parked,
epic, open PR, hard blockers), topological ordering, and the cap. Run:

```bash
python3 -m unittest discover -s .claude/skills/pipeline-dev/tests
```
