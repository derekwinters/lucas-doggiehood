---
name: pipeline-gatekeeper
description: >
  Translate Derek's (repo-owner) issue comment commands into pipeline label
  moves for derekwinters/lucas-doggiehood. In production this runs as two
  deterministic GitHub Actions workflows — no LLM —
  `.github/workflows/gatekeeper-comment.yml` (instant, per-issue, on
  `issue_comment`) and `.github/workflows/gatekeeper-sweep.yml` (board-wide
  `check_revisits` + `reconcile`, event + cron backstop). This SKILL.md
  documents the same deterministic logic for manual/on-demand invocation —
  use when asked to "run the gatekeeper" / process pipeline commands outside
  the workflows. Owner-only (bad-actor gate), idempotent via a reaction
  watermark, deterministic parsing via parse_commands.py.
---

# Pipeline gatekeeper

The gatekeeper is the **only** thing that turns Derek's comments into pipeline
state. Labels are the state machine; comments are the control surface; this
skill is the translator.

**Since issue #319, the gatekeeper is no longer a step inside the scheduled AI
routines** — it runs as two always-on GitHub Actions workflows instead (see
`docs/engineering/issue-pipeline.md` → "Routines and the dashboard workflow"):
`gatekeeper-comment.yml` applies Derek's `/commands` the instant he posts
them, and `gatekeeper-sweep.yml` runs the board-wide auto-revisit and
reconcile sweep on close/label/merge events plus a low-frequency cron
backstop. Both wrap the exact same deterministic Python this SKILL.md
describes (`parse_commands.py`, `check_revisits.py`,
`pipeline-reconcile/reconcile.py`, plus the fetch/apply glue below) — this
document remains the reference for that logic, and for running it by hand
when asked to.

See `docs/engineering/issue-pipeline.md` for the full model.

## Non-negotiables

1. **Owner-only.** Only comments whose author is the repo owner
   (`derekwinters`) are honored. Everyone else's `/commands` are ignored — this
   is the bad-actor gate. The parser enforces this; never override it.
2. **Idempotent.** A processed comment is marked with a 👀 (`eyes`) reaction —
   the **watermark**. Comments already carrying the watermark are skipped, so
   re-running with no new comments changes nothing.
3. **Deterministic parsing.** Command parsing lives in `parse_commands.py`. The
   model's only job is to gather the snapshot, apply the actions the script
   returns, and write short acknowledgment text. Do not re-interpret commands
   in prose.
4. **Never touch** `type:epic` issues or the `dashboard` issue (#193).

## Command vocabulary

| Command | Effect |
| - | - |
| `/admit` | add `ai-triage` (raw idea → analysis queue) |
| `/approve` | add `ready-for-work`, remove `pending-approval`/`needs-clarification`/`ai-triage` — **refused if the issue has no milestone set** (`ready-for-work` ⇒ has milestone, #247; a pure presence-check, issue #319) |
| `/revise <notes>` | re-add `ai-triage`, remove `pending-approval`/`needs-clarification`; the notes are left for analysis to read |
| `/redo` | re-add `ai-triage`, remove `pending-approval`/`needs-clarification` (fresh analysis pass) |
| `/propose` | re-add `ai-triage` and authorize analysis to draft the missing design as a marked PROPOSAL |
| `/park` / `/unpark` | add / remove `parked` |
| `/milestone <name>` | set the milestone (accepts `04`, a title fragment, or the full title) |
| `/focus <name>` | record the active nightly-dev milestone (stored in the dashboard marker — see below) |
| `/cap <n>` | set the nightly dev build cap (**dashboard issue only** — see below); rejects non-numeric or non-positive `n` |

A `parked` issue only responds to `/unpark`.

## Where `/focus` and `/cap` are stored

The active nightly-dev milestone and the nightly build cap each live in a
**hidden marker on the dashboard issue (#193)** body:

```
<!-- pipeline-focus: 04 - Quests & Economy -->
<!-- pipeline-cap: 3 -->
```

This is the single source of truth read by both `pipeline-dev` (queue
selection) and the dashboard workflow. It was chosen over a committed state
file so no routine needs to push a commit just to record focus/cap, and over a
separate issue so the value sits next to where it's displayed. When `/focus`
or `/cap` fires, update the corresponding marker (see step 3). If the focus
marker is absent, focus defaults to the lowest-numbered milestone with open
`ready-for-work` issues; if the cap marker is absent, the cap defaults to
**3** (the same default `select_queue.py` itself falls back to).

Unlike `/focus`, which is honored from **any** issue as well as the
dashboard, `/cap` is honored **only** on the dashboard issue (#193) — it is
silently ignored everywhere else.

## Procedure

1. **Gather the snapshot.** With the GitHub MCP tools, list open issues in
   `derekwinters/lucas-doggiehood` (exclude none yet — the script filters
   epics/dashboard). For each issue collect: `number`, `labels`, whether it is
   `type:epic` (`is_epic`), whether it is #193 (`is_dashboard`), its current
   `milestone` title (or null — this is the ONLY thing that feeds the
   `/approve` milestone gate below; analysis now sets this field directly at
   `pending-approval`, issue #319 Part A, so there is no separate
   proposed-milestone comment to scrape), and its comments. For each comment
   collect `id`, `author.login`, `body`, and `processed` = whether it already
   carries the 👀 `eyes` reaction from this bot. To keep this cheap, only fetch
   comments for issues that actually have any (skip issues with `comments ==
   0`), and only look back at recent comments.

2. **Run the parser.** Pipe the snapshot JSON into the script:

   ```bash
   python3 .claude/skills/pipeline-gatekeeper/parse_commands.py < snapshot.json
   ```

   Provide `repo_owner: "derekwinters"` and `milestones` (the live list of
   open milestone titles, from the milestones API — see
   `docs/engineering/issue-pipeline.md` → "Fetching live milestones" for the
   exact recipe) in the payload. The script returns
   `{"actions": [...], "skipped": [...]}`.

3. **Apply each action** with the GitHub MCP tools, in the order returned:
   - Add/remove the labels in `add_labels` / `remove_labels`.
   - If `set_milestone` is non-null, set that milestone.
   - **`/approve` milestone gate (`ready-for-work` ⇒ has milestone, #247):**
     the parser now enforces this as a pure **presence-check** on the
     `milestone` field you gathered (issue #319, Part A) — no resolution, no
     name→number matching, no comment-scraping, and an inline `/milestone` in
     the same comment does **not** feed this gate (it fires as its own,
     separate action instead — see below). You do **not** re-decide any of
     this in prose. When the issue already has a milestone, `/approve` needs
     no milestone write of its own (`set_milestone` stays null on that
     action) — it is already correct. When the field is null, the parser
     emits **no** approve action and instead a `{"reason":
     "approve-no-milestone", "menu": "which-milestone"}` skip — the issue
     stays in its prior state; post the which-milestone hand-back (see below)
     rather than moving it to `ready-for-work`. (If the same comment also
     contained a `/milestone` command, that command's own action still
     applies — just not as part of this approve.)
   - If `set_focus` or `set_cap` is non-null, update the corresponding
     `<!-- pipeline-focus: ... -->` / `<!-- pipeline-cap: N -->` marker on #193
     by **re-rendering** the dashboard with a `DASHBOARD_SET_FOCUS` /
     `DASHBOARD_SET_CAP` override — never hand-edit the marker into #193's body
     directly (a read-modify-write re-HTML-encodes it and breaks the Mermaid
     charts, the failure mode `/focus` hit in [#204](https://github.com/derekwinters/lucas-doggiehood/issues/204)):

     ```bash
     DASHBOARD_SET_FOCUS='<milestone>' python3 .claude/skills/pipeline-dashboard/render_dashboard.py --write
     DASHBOARD_SET_CAP='<n>' python3 .claude/skills/pipeline-dashboard/render_dashboard.py --write
     ```

     The renderer writes the new marker itself (raw) as part of the freshly
     rendered body. In the deterministic workflow, `run_comment_event.py` does
     this automatically for any processed action carrying `set_focus` /
     `set_cap` (`_rerender_dashboard`); restoring the `/focus` override closed
     the parked [#204](https://github.com/derekwinters/lucas-doggiehood/issues/204) / [#234](https://github.com/derekwinters/lucas-doggiehood/issues/234) gap.

4. **Acknowledge.** React to the source comment with 👍 (`+1`) to confirm the
   action, and — where it moves the issue to a state awaiting Derek — post a
   short comment ending with the `menu` the action names (see `MENUS` in the
   script for the exact "Your move" text). Keep acknowledgments to one or two
   lines; the deterministic work is already done. For an
   **`approve-no-milestone`** skip (an `/approve` the parser refused because no
   milestone resolved), react 👍 and post the which-milestone hand-back — a
   single line like `Can't approve #N to ready-for-work — no milestone
   resolved; reply` followed by the skip's `which-milestone` menu — so Derek
   sees the issue stayed put and knows to set a milestone first.

5. **Watermark.** Add the 👀 `eyes` reaction to every comment you processed
   (both honored and owner-authored no-ops) so the next run skips it. This is
   what makes the gatekeeper idempotent — do not skip it.

6. **Auto-revisit unblocked questions** (`check_revisits.py`, issue #241). An
   issue can sit in `needs-clarification` only because it is `Blocked by: #N` —
   it needed a decision that lives in its blocker. This is a **state-derived
   transition, not a comment command**, so it runs here after the
   comment-driven moves above, once those labels are set. Build a snapshot of
   the **open** issues (each `number`, `labels`, `body` — the body carries the
   structured `Blocked by: #N` lines — and `native_blocked_by`, the issue's
   native GitHub issue-dependency hard blockers read from
   `GET /repos/{owner}/{repo}/issues/{n}/dependencies/blocked_by`, #321) and pipe
   it in:

   ```bash
   python3 .claude/skills/pipeline-gatekeeper/check_revisits.py < snapshot.json
   ```

   The script returns `{"revisits": [...]}`. For each revisit, apply its
   `add_labels` / `remove_labels` (swap `needs-clarification` → `ai-triage`) and
   post a short auto-comment naming the cleared blocker(s) and ending in the
   `back-to-analysis` menu — e.g. *"Blocker #N reached `ready-for-work` —
   revisiting."* A blocker counts as resolved when it is closed/merged (absent
   from the open snapshot) or carries `ready-for-work`/`in-progress`; an issue
   with **multiple** blockers only revisits once **all** are resolved. Hard
   blockers are the union of structured `Blocked by: #N` lines and native
   relationships (`native_blocked_by`), so a natively-recorded blocker gates and
   clears a revisit too (#321); a prose mention never fires it, and soft
   `Depends on:` (no native form) never gates a revisit.

7. **Run the reconciliation sweep** (`pipeline-reconcile`) against live state,
   now that the commands above have set their labels. Apply only its
   `strip_labels` and `requeue` auto-fixes; the sweep never closes an issue, and
   its `flag_*` findings are surfaced by the dashboard, not acted on here. See
   `docs/engineering/issue-pipeline.md`.

8. **Report** a one-line summary per issue touched (e.g.
   `#181 approve → ready-for-work (07 - Polish & Onboarding)`, or
   `#185 revisit → ai-triage (blocker #109 cleared)`), and note any `skipped`
   non-owner commands so Derek can see an attempted bad-actor command was
   ignored.

## Automated workflows (issue #319)

The procedure above is what the two GitHub Actions workflows automate,
without a model in the loop:

- **`fetch_comment_event.py`** — pure function `build_snapshot(event,
  repo_owner, milestones)` that turns one raw `issue_comment` webhook event
  payload into the single-issue, single-comment snapshot `parse_commands.process`
  already consumes as a one-element `issues` list. Skips PR comments
  (`"pull_request" in issue`) and bot-authored comments before ever reaching
  the parser.
- **`apply_actions.py`** — pure functions that turn one `parse_commands`
  action (or skip) into concrete write instructions: `merge_labels` (the full
  label list to PATCH — GitHub's labels endpoint replaces the whole set, so
  add/remove must be merged against a freshly read current list),
  `render_ack` / `render_skip_ack` (the acknowledgment text, from `MENUS`),
  `reactions_for` (👍 + 👀), `milestone_write_for` (only ever non-null for
  an actual `/milestone` command — never for `/approve`, per Part A), and
  `fires_triage` (the reactive-triage transition detector, #378 — true only
  when a label change *newly* adds `ai-triage`).
- **`fire_routine.py`** — the reactive-triage hook (#378): when `fires_triage`
  is true, `fire(issue, repo)` makes a best-effort outbound POST to a Claude
  Code Routine `/fire` endpoint (`AI_TRIAGE_URL` / `AI_TRIAGE_SECRET` repo
  secrets) so the analysis routine runs for that one issue immediately instead
  of waiting for the 7:00 AM backstop. `build_fire_request` is the pure,
  unit-tested half; a missing secret or a network error is a clean no-op — the
  label move already happened. See `docs/engineering/issue-pipeline.md`.
- **`run_comment_event.py`** — the `gatekeeper-comment.yml` entry point: reads
  `GITHUB_EVENT_PATH`, re-checks the owner-gate in-script (defense-in-depth),
  fetches live open milestones, then wires `fetch_comment_event` →
  `parse_commands.process` → `apply_actions` → the GitHub REST API, and fires
  reactive triage (`fire_routine.fire`) whenever a command newly adds
  `ai-triage`.
- **`run_sweep.py`** — the `gatekeeper-sweep.yml` entry point: runs
  `check_revisits` + `reconcile` board-wide (in `events_only` mode unless
  invoked with `--cron`), and on `--cron` additionally re-processes any
  `issue_comment` command a dropped webhook made the primary workflow miss.
  Fires reactive triage for a blocker-cleared revisit (or a replayed missed
  command) that newly adds `ai-triage`.
- **`_github_api.py`** — the tiny shared `urllib` request helper both of the
  above import.

The last three are pure network glue over the tested functions above (not
themselves unit-tested — the same convention as `reconcile.fetch_state` /
`render_dashboard.py`'s live-fetch/write halves).

## Tests

`tests/test_parse_commands.py` covers owner-only gating, the watermark,
epic/dashboard exclusion, each command's label move, milestone matching, the
`/approve` milestone gate (`ready-for-work` ⇒ has milestone — a pure
presence-check on the issue's milestone field, issue #319: refused with an
`approve-no-milestone` skip when it is null, honored when it is already set;
locks in that an inline `/milestone` in the SAME comment no longer feeds this
gate — it fires only as its own separate action), the parked-issue rule, and
`/cap` (#240: honored only on the dashboard issue, resolves to `set_cap`,
rejects non-numeric/non-positive input with `cap-invalid`, ignored on every
other issue).
`tests/test_check_revisits.py` covers
the blocker auto-revisit (#241): single/multiple blockers, closed vs.
`ready-for-work`/`in-progress` blockers, the all-must-resolve rule, native-only
blockers and the native∪text union (#321), and the regression guards (no
`Blocked by:` line and no native blocker, still-open blocker, prose-only
mention, non-`needs-clarification` and `parked` issues never fire).
`tests/test_fetch_comment_event.py` (#319) covers `build_snapshot`: the
one-element snapshot shape, PR-comment and bot-comment skips, epic/dashboard
flag derivation, current-milestone carry-through, and that its output feeds
`parse_commands.process` to the same result as an equivalent hand-authored
fixture. `tests/test_apply_actions.py` (#319) covers `merge_labels` (add +
remove, no-op, no duplicate add, removing an absent label), `render_ack` /
`render_skip_ack`, `reactions_for`, and `milestone_write_for` (null on
`/approve`, set on `/milestone`). Run:

```bash
python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests
```
