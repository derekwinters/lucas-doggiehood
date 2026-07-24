---
name: pipeline-reconcile
description: >
  Reconciliation sweep for the Doggiehood issue pipeline. Deterministically
  detects issues that have drifted out of the label state machine — closed
  issues still carrying pipeline-state labels, stalled in-progress issues with
  no PR, and merged-but-open work on main — then auto-fixes the safe cases and
  flags the ambiguous ones on the dashboard. Runs in the gatekeeper step of
  each scheduled routine, after command processing.
---

# Pipeline reconcile — the drift sweep

The pipeline's labels are a state machine, but nothing guarantees an issue stays
inside it. Issues fall out silently: a merged PR leaves its issue `open`, a
nightly build drops an `in-progress` issue's commits and it never re-enters the
queue, a `Closes #N` closes the issue but leaves `in-progress` attached. The
reconciliation sweep is the periodic check that catches this drift. See
`docs/engineering/issue-pipeline.md`.

Same split as the other deterministic skills: **detection is a pure,
unit-tested function** (`reconcile.py::process`); GitHub I/O lives only at the
edges (`fetch_state`, and the gatekeeper/dashboard that apply or surface the
findings).

## Non-negotiables

1. **Never closes an issue.** The sweep only strips stale labels and requeues
   stalled issues. Merged-but-open issues are **flagged**, never auto-closed —
   `#211` owns auto-close-on-merge, and the done-ness heuristic can
   false-positive.
2. **Done-ness is decided by a merged commit *body* reference (or deliverables
   on `HEAD`), never a PR/commit *title*.** The nightly builder squash-merges
   several issues under one lead PR title, so a title-only match keeps missing
   bundled squashes (verified on #109/#58/#57/#190/#170 — see #246). This guard
   is locked in by `test_title_only_reference_does_not_flag_done`.
3. **Deterministic detection.** All classification lives in `process`; the model
   only gathers the snapshot and applies the actions the script returns.

## Detection rules and auto-fix vs. flag

| Rule | Condition (deterministic) | Action |
| - | - | - |
| **Closed + stale label** | issue `closed` yet still carrying any of `ai-triage`, `pending-approval`, `needs-clarification`, `ready-for-work`, `in-progress` | **auto-fix** → `strip_labels` (mirror of merged-but-open; the `Closes #N` label-leak seen on #211 itself) |
| **Stalled `in-progress`** | open, `in-progress`, **no open PR**, **not** on `main` | **auto-fix** → `requeue` (`in-progress` → `ready-for-work`) so the builder retries |
| **Merged-but-open** (incl. bundled squash) | open, work **is** on `main` (merged-commit-body ref or deliverables present) | **flag** → `flag_done`; surfaced on the dashboard for Derek to close, *not* auto-closed |
| **Orphaned ready** (stretch) | open, `ready-for-work`, **no milestone** | **flag** → `flag_orphaned_ready` |
| **Prose-only dependency** (stretch) | open, body has a prose "blocked by/depends on #N" not in structured form | **flag** → `flag_prose_dep` |

Why this split: the two auto-fixes are **safe and unambiguous** — a closed issue
must not keep a pipeline-state label, and a stalled issue with no work on `main`
belongs back in the queue. Everything about *closing* an issue is either
ambiguous (the done-ness heuristic) or already owned by #211, so it is flagged
for a human, not applied.

The classification split matters: an open `in-progress` issue that **is** on
`main` is `flag_done`, never `requeue` — that is the guard that stops the #109
re-pick loop where the builder kept picking up already-done work.

`type:epic` issues, the dashboard issue (#193), and `parked` issues are excluded
throughout, matching the rest of the pipeline.

## Procedure (in the gatekeeper routine, after command processing)

1. **Gather the snapshot** with the GitHub MCP tools (or run `reconcile.py
   --live` with `GITHUB_TOKEN` set, which does it via stdlib `urllib`):
   - every issue with `number`, `state`, `labels`, `milestone`, `is_epic`,
     `is_dashboard`, and `has_open_pr` (an **open** PR references it);
   - `merged_commit_body_refs`: issue numbers referenced by `#N` / `Refs #N` /
     `Closes #N` in a merged commit **body** reachable from `main`;
   - optionally `deliverables_present` (a `{ "N": true }` map when the fetch
     layer can cheaply confirm an issue's Build-checklist files exist at `HEAD`);
   - optionally `prose_deps` per issue for the stretch flag.

2. **Classify** deterministically:

   ```bash
   python3 .claude/skills/pipeline-reconcile/reconcile.py < snapshot.json
   # or, headless with GITHUB_TOKEN set:
   python3 .claude/skills/pipeline-reconcile/reconcile.py --live
   ```

   Returns `strip_labels`, `requeue`, `flag_done`, `flag_orphaned_ready`,
   `flag_prose_dep` — each a list sorted by issue number.

3. **Apply the auto-fixes** with the GitHub MCP tools — and only these:
   - `strip_labels`: remove exactly the named labels from each closed issue.
   - `requeue`: on each issue, remove `in-progress`, add `ready-for-work`.
   Never close an issue and never touch a `flag_*` issue's labels here.

4. **Leave the flags for the dashboard.** The dashboard renderer runs the same
   sweep and lists the `flag_*` findings in its read-only "⚠️ Reconcile"
   section (see `pipeline-dashboard`). No action from the gatekeeper beyond the
   two auto-fixes.

5. **Report** a one-line summary per issue touched (e.g.
   `#211 strip in-progress (closed)`, `#109 requeue → ready-for-work`) and the
   flag counts.

## Coordination

- Detection is shared: `pipeline-dashboard`'s renderer imports this skill's
  `process`/`fetch_state` to populate its Reconcile section, so the flags on the
  dashboard and the auto-fixes in the gatekeeper come from one implementation.
- Runs **after** command processing in the gatekeeper so it reconciles against
  the labels those commands just set.

## Tests

`tests/test_reconcile.py` covers each rule with a representative fixture and its
negative counterpart — closed+stale strip, stalled requeue, merged-but-open via
commit body, the title-only guard, the done-vs-stall classification split, the
stretch flags, the epic/dashboard/parked exclusions, and the healthy/empty
board. Run:

```bash
python3 -m unittest discover -s .claude/skills/pipeline-reconcile/tests
```
