---
name: pipeline-dev
description: >
  Nightly serial builder for the Doggiehood issue pipeline. Selects the
  eligible ready-for-work issues in the focus milestone, builds each with the
  doggiehood-dev agent (strict TDD) onto one shared branch, and opens a single
  combined PR. Never merges, never closes. Use in the nightly development
  routine, after the gatekeeper has run.
---

# Pipeline dev — nightly serial builder

Wraps the existing `doggiehood-dev` agent with queue selection, a serial build
loop, and combined-PR assembly. Runs in the **nightly** routine (1 AM CT),
after the gatekeeper. See `docs/engineering/issue-pipeline.md`.

## Non-negotiables

1. **Never merges, never closes.** This skill opens exactly one PR and stops.
   Derek reviews and merges; PR-babysitting keeps it green.
2. **Strict TDD per issue** — every issue is built by the `doggiehood-dev`
   agent, which enforces red-green-refactor. Do not bypass it.
3. **Keep the branch green.** If an issue fails to build cleanly, drop its
   commits so the shared branch stays buildable, and continue to the next.
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

4. **Create one shared branch** off the latest `main`
   (e.g. `pipeline/nightly-YYYYMMDD`).

5. **Serial build loop** — for each issue in `selected`, in order:
   - Run the `doggiehood-dev` agent on that single issue, committing onto the
     shared branch with a Conventional Commit message that references the issue
     (`Refs #NN`). Mark the issue `in-progress`.
   - If the agent cannot make it pass (tests red, blocked, or it flags a
     docs/spec gap): **drop that issue's commits** (`git reset --hard` back to
     the last good commit) so the branch stays green, remove any `in-progress`
     it added back to `ready-for-work`, and record the reason. Continue.

6. **Open one combined PR** (never merge):
   - **Title** = the lead (first built) issue's Conventional line, e.g.
     `feat: give approach-to-rest real walk-to-decoration movement`.
   - **Body** starts with the required `## Deviations and Decisions` section
     (per `docs/engineering/agent-workflow.md`), then a list of **raw**
     Conventional-commit lines — one per built issue, no leading `*` or `-` —
     so release-please emits one changelog entry per issue on squash-merge.
     See `docs/engineering/versioning.md`. Example body block:

     ```
     feat: add decline action to the conversation panel (#185)
     fix: make the lost-dog sphere reachable (#181)
     ```

   - List any dropped issues and why, and any `capped_out` issues deferred to
     the next night. **Log the cap explicitly** so a truncated queue never
     reads as "everything was built."

7. **Babysit** the PR to keep CI green (the standard PR-activity flow); do not
   merge or close.

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
