# AI Issue-Management Pipeline

*Issue: [#191](https://github.com/derekwinters/lucas-doggiehood/issues/191)*

A label-driven pipeline moves issues from raw idea → analysis → Derek's
approval → nightly development, driven by scheduled Claude Code routines plus
one deterministic dashboard workflow.

## The model

**Labels are the state machine. Comments are Derek's control surface. A
"gatekeeper" translates comments into label moves.** Only the repo owner's
comments (`derekwinters`) are honored — the bad-actor gate. Everyone else's
`/commands` are ignored.

### States (labels)

| Label | Set by | Meaning |
| - | - | - |
| *(none)* | anyone | Raw idea. Ignored by the AI; shows in the dashboard intake. |
| `ai-triage` | gatekeeper (on `/admit`) | Admitted; queued for analysis. |
| `pending-approval` | analysis | Bug diagnosis / spec-covered plan posted; awaiting `/approve`. |
| `needs-clarification` | analysis | A clearly-stated question is on the issue; awaiting an answer. |
| `ready-for-work` | gatekeeper (on `/approve`) | Approved + milestone set; in the dev queue. **Invariant: `ready-for-work` ⇒ the issue has a milestone** ([#247](https://github.com/derekwinters/lucas-doggiehood/issues/247)) — the gatekeeper refuses any `/approve` that would land here milestone-less, so the nightly builder (which only sees the focus milestone) never silently skips an approved issue. |
| `in-progress` | dev | A nightly dev run picked it up / opened its PR. |
| `parked` | gatekeeper (on `/park`) | Hidden from every routine and the dashboard, any stage, indefinitely. |
| `dashboard` | one-time | Marks the dashboard issue ([#193](https://github.com/derekwinters/lucas-doggiehood/issues/193)); excluded everywhere except `/focus`, which is honored on it so focus can be set from the dashboard ([#204](https://github.com/derekwinters/lucas-doggiehood/issues/204)). |

`type:epic` issues are excluded from admit/dev throughout.

### Commands

Comment on any issue (prose around the command is fine — only the owner's
commands act):

| Command | Effect |
| - | - |
| `/admit` | Pull a raw idea into AI analysis (`ai-triage`). |
| `/approve` | Accept the analysis → `ready-for-work`, set the proposed (or `/milestone`-overridden) milestone. **Refused if no milestone resolves** (see the `/approve` milestone gate below). |
| `/revise <notes>` | Send back to analysis with feedback (re-add `ai-triage`). |
| `/redo` | Discard the analysis and start it over. |
| `/propose` | Authorize analysis to draft the missing design/wireframe as a marked PROPOSAL. |
| `/park` / `/unpark` | Hide from the pipeline / bring it back. |
| `/milestone <name>` | Override the milestone (`04`, a title fragment, or the full title). |
| `/focus <name>` | Set the active milestone for nightly development. |
| `/cap <n>` | Set the nightly dev build cap. **Dashboard issue (#193) only** — rejects non-numeric or non-positive `n`. |

Every AI hand-back comment ends with the context-appropriate "Your move" menu.

### The bad-actor gate and idempotency

The gatekeeper honors a command only if the comment's author is the repo owner.
Processed comments are watermarked with a 👀 reaction so re-running a routine
never double-applies a command. Both rules are enforced by the deterministic
parser (`.claude/skills/pipeline-gatekeeper/parse_commands.py`), not by model
judgment.

### The `/approve` milestone gate — `ready-for-work` ⇒ has milestone

Nightly development only builds issues in the **focus milestone**, so a
`ready-for-work` issue with **no milestone** is invisible to the builder —
"approved and ready" yet silently never picked up. To close that gap
([#247](https://github.com/derekwinters/lucas-doggiehood/issues/247)) the parser
enforces the invariant **`ready-for-work` ⇒ the issue has a milestone**.

On `/approve` the parser resolves an **effective milestone** from, in order: an
inline `/milestone` in the same comment, the issue's current milestone, then the
milestone analysis proposed in its `pending-approval` comment (the snapshot
carries `milestone` and `proposed_milestone` for exactly this). Resolution is
done as an order-independent finalization, so `/approve\n/milestone 07` and
`/milestone 07\n/approve` behave identically.

- **A milestone resolves** → the move proceeds and that milestone rides along on
  the action, so `ready-for-work` is never set milestone-less.
- **No milestone resolves** → the transition is **refused**: no label change,
  the issue stays in its prior state (`pending-approval` / `needs-clarification`),
  and the parser emits an `approve-no-milestone` skip carrying a
  `which-milestone` hand-back menu. The gatekeeper posts a "which milestone?"
  reply (e.g. *"Can't approve #N to `ready-for-work` — no milestone resolved;
  reply `/milestone <name>` then `/approve`"*).

This is the **presence** gate. Its sibling
[#212](https://github.com/derekwinters/lucas-doggiehood/issues/212) layers an
**order** gate onto the same resolved value (the milestone must not precede a
blocker's milestone); the two chain **resolve → presence (#247) → order (#212)**,
so the order check only runs once a milestone exists.

### Auto-revisit when a blocker clears

An issue can be parked in `needs-clarification` **only because** it is
`Blocked by: #N` — it needed a decision that lives in its blocker (e.g. issue 1
blocks issue 2; issue 2 was sent back for clarification only because it awaits a
call on issue 1). Nothing was re-examining those issues once the blocker
resolved: analysis only acts on `ai-triage`, and the gatekeeper otherwise only
acts on Derek's comments — so the question stalled indefinitely
([#241](https://github.com/derekwinters/lucas-doggiehood/issues/241)).

The gatekeeper closes that gap with a **state-derived transition** (not a
comment command): the deterministic `check_revisits.py::check_blocker_revisits`
takes the open-issue snapshot (number, labels, body) and returns a revisit —
add `ai-triage`, remove `needs-clarification` — for every `needs-clarification`
issue whose structured `Blocked by: #N` blockers have **all** resolved. A
blocker is resolved when it is closed/merged (absent from the open snapshot) or
carries `ready-for-work`/`in-progress`. An issue with multiple blockers revisits
only once every one is resolved; an issue with no structured `Blocked by:` line,
a still-open unresolved blocker, a prose-only mention, or a `parked` label is
never touched (no false triggers). It runs in the gatekeeper step **after**
comment-driven moves, so it reconciles against the labels those commands just
set, and posts a short auto-comment naming the cleared blocker(s) ending in the
`back-to-analysis` menu.

## Where `/focus` and `/cap` are stored

The active nightly-development milestone and the nightly build cap
([#240](https://github.com/derekwinters/lucas-doggiehood/issues/240)) each
live in a hidden marker on the **first two lines of the dashboard issue (#193)
body**:

```
<!-- pipeline-focus: v0.4 -->
<!-- pipeline-cap: 3 -->
```

This is the single source of truth shared by the gatekeeper (sets them on
`/focus` / `/cap`), `pipeline-dev` (reads them to pick the queue and the
cap), and the dashboard renderer (reads them, displays them, and re-emits
them). It was chosen over a committed state file (no routine needs to push a
commit just to record focus/cap) and over a separate issue (the value sits
next to where it's shown). If the focus marker is absent, focus defaults to
the lowest version milestone with open `ready-for-work` issues; if the cap
marker is absent, cap defaults to **3** (matching `select_queue.py`'s own
`cap = data.get("cap", 3)` fallback).

`/cap` sets its marker by **re-rendering the dashboard** with a
`DASHBOARD_SET_CAP` override (`render_dashboard.py::_resolve_cap`: override →
marker → default) — it never hand-edits #193's body directly, so it cannot hit
the HTML-entity/Mermaid corruption bug described below. Unlike `/focus`,
`/cap` is honored **only** on the dashboard issue; it is silently ignored
everywhere else.

The gatekeeper sets focus by **re-rendering the dashboard** with a
`DASHBOARD_SET_FOCUS` override, which writes the new marker into a freshly
rendered (raw) body — it never hand-edits #193's body directly. Reading and
writing that body back through the GitHub tools re-HTML-encodes it (`"` →
`&#34;`, `&` → `&amp;`) and breaks the Mermaid charts
([#204](https://github.com/derekwinters/lucas-doggiehood/issues/204)).

> **Note (found while building #240):** the `DASHBOARD_SET_FOCUS` override and
> its `_resolve_focus` precedence, described above and originally shipped in
> [#230](https://github.com/derekwinters/lucas-doggiehood/pull/230), are no
> longer present in `render_dashboard.py` — they were dropped when the shared
> `ai-skills` pipeline bundle was adopted
> ([#238](https://github.com/derekwinters/lucas-doggiehood/pull/238)) without
> this doc being updated to match. `/cap`'s `DASHBOARD_SET_CAP` override was
> implemented fresh for #240 following this same documented pattern and *is*
> present in code today (see `_resolve_cap`). Restoring `/focus`'s override is
> a pre-existing gap outside #240's scope — flagged here rather than fixed
> silently; it should be filed as its own follow-up issue.

`/focus` is now honored on the dashboard issue itself, and a `/focus` naming a
milestone that matches no live milestone is rejected rather than silently
stored. Relocating the marker out of the dashboard body into a dedicated
single-writer store is tracked upstream in
[ai-skills#8](https://github.com/derekwinters/ai-skills/issues/8).

## Fetching live milestones

GitHub's MCP toolset exposed to this pipeline has **no dedicated
milestone-list or milestone-set tool** — don't spend time hunting for one that
doesn't exist.

The reliable recipe is a direct call to the milestones REST endpoint:

```
GET https://api.github.com/repos/derekwinters/lucas-doggiehood/milestones?state=open&per_page=100
```

Issue this via `WebFetch` (or an equivalent authenticated GitHub API call).
The response is clean JSON giving each open milestone's `number`, `title`,
and `description`.

`issue_write`'s `milestone` parameter takes the milestone's **`number`**, not
its `title`. Match the issue against the live `title`/`description` text
first, then use that milestone's `number` when writing.

Prefer this JSON endpoint over WebFetching the HTML `/milestones` page: the
HTML page needs JS rendering to enumerate milestones and has been observed to
under-report — dropping closed-milestone titles from the same fetch
([#227](https://github.com/derekwinters/lucas-doggiehood/issues/227)).

## Routines and the dashboard workflow

The gatekeeper runs first in each AI routine so downstream stages see fresh
labels.

| Time (CT / UTC) | Runner | Does |
| - | - | - |
| 7:00 AM / `0 12 * * *` | AI routine | gatekeeper → analysis |
| 6:00 PM / `0 23 * * *` | AI routine | gatekeeper (review refresh) |
| 1:00 AM / `0 6 * * *` | AI routine | gatekeeper → dev |
| 13:00, 00:00, 07:00 UTC | **Actions workflow** | dashboard render (`.github/workflows/dashboard.yml`) |

Fixed-UTC cron drifts one hour across US daylight-saving changes — accepted and
noted here rather than worked around.

**Why the dashboard is a workflow, not an AI step.** The dashboard body is a
pure function of repo state, so it is rendered by a deterministic script on a
GitHub Actions schedule — no model in the loop. That is cheaper, byte-stable,
and authenticates its headless PATCH with the built-in `GITHUB_TOKEN`, so no
extra secret is needed. The workflow runs ~1 hour after each AI routine so it
reflects the gatekeeper's label moves. (The original epic folded the dashboard
into each AI routine; this pivot supersedes that.)

## Stage behavior

### Analysis (`pipeline-analysis`)

Digs into every `ai-triage` issue in parallel and routes it to await Derek —
**never inventing design**:

- **Bug** → root-cause diagnosis + fix approach, ending with a `## Build
  checklist` of acceptance criteria → `pending-approval` (adds `type:bug`).
- **Spec-covered feature** → implementation plan + a milestone proposed by
  matching **live milestone descriptions** + a closing `## Build checklist` of
  acceptance criteria → `pending-approval`.
- **Feature needing a new design call or a UI wireframe** (CLAUDE.md rule #8) →
  **stops and asks** with a concrete `❓ Needs from Derek/Lucas:` question →
  `needs-clarification` (no plan or checklist).
- **`/propose` set** → authorized to draft the design as a marked PROPOSAL, with
  a `## Build checklist` of acceptance criteria → `pending-approval`.

The `## Build checklist` is TDD-ordered checkbox acceptance criteria seeded from
the relevant `docs/specs/**` page's own build checklist — what Derek approves
and the reviewer checks the PR against.

Dependencies are recorded as first-class GitHub relationships — sub-issues for
decomposition, `Blocked by: #N` for hard peer dependencies, `Depends on: #N`
for soft sibling ordering. See **Recording dependencies** below.

### Recording dependencies

**Every dependency between issues is recorded as a structured relationship —
never as prose alone** ([#248](https://github.com/derekwinters/lucas-doggiehood/issues/248)).
The nightly builder (`select_queue.py`) and the dashboard derive an issue's
blockers and ordering **only** from structured forms; a dependency written in a
sentence (e.g. "this depends on #109") is invisible to them, so the builder sees
the issue as unblocked and can build it before its prerequisite exists. That is
the exact drift that motivated this rule — several issues (#57, #170, #185) had
prose-only deps and were treated as eligible.

There are exactly **two** supported ways to record a dependency:

1. **A structured line** in the issue body — one reference per line, the keyword
   followed by a colon and `#N`:
     - `Blocked by: #N` — **hard gate.** The dependent is ineligible for dev
       until `#N` is closed/merged (parsed into `blocked_by`).
     - `Depends on: #N` — **soft ordering.** The dependent may build, but a
       prerequisite sorts first (parsed into `depends_on`).
   The colon is what makes the line canonical/structured; a keyword mention
   without it (e.g. `blocked by #57` mid-sentence) is prose, not a structured
   line. The hard-vs-soft semantics are settled in
   [#197](https://github.com/derekwinters/lucas-doggiehood/issues/197).
2. **A native GitHub relationship** — a real issue-dependency or a sub-issue
   parent link, where the tooling can read it.

Writing the issue number in a sentence is **not** sufficient even when the prose
already names it — the structured line (or native relationship) must be present.
The reconciliation sweep enforces this: it flags any open issue whose body
mentions `depends on #N` / `blocked by #N` in prose with no matching structured
line for that number (see **Prose-only dependency** below).

### Development (`pipeline-dev`)

A serial nightly builder wrapping the `doggiehood-dev` agent. It builds the
eligible set — `ready-for-work` **and** in the focus milestone **and** all hard
blockers closed/merged **and** not `parked` **and** no open PR — in topological
order (dependencies first, then issue number), up to the nightly cap read from
the `<!-- pipeline-cap: N -->` marker on #193 (default **3**, settable with
`/cap <n>` — see [#240](https://github.com/derekwinters/lucas-doggiehood/issues/240)).
Each issue is built on **its own branch** and opened as **its own PR**
(title = that issue's single Conventional line; body = `## Deviations and
Decisions` + `Closes #N`); a failing issue is dropped — its branch deleted, no
PR — and the loop continues. Because each PR resolves exactly one issue, its
squash-merge lands as one Conventional Commit and release-please emits one clean
changelog entry per issue, and the `Closes #N` keyword auto-closes the issue on
merge. Each built issue is marked `in-progress`. It **never merges and never
closes** — Derek reviews and merges; PR-babysitting keeps CI green.

When a built PR touches no `docs/**` page, `pipeline-dev` applies the
`skip-docs` label **immediately after** opening it, before any other post-open
work. The label can't be set atomically at PR creation, so the `docs-test` gate
absorbs the brief `opened`→`labeled` gap with a live-label grace poll rather than
firing a transient failure on the `opened` run
([#254](https://github.com/derekwinters/lucas-doggiehood/issues/254)); see
[CI/CD](ci-cd.md#docs-site-build-publish). A PR that reconciles docs needs no
label — the gate passes on the docs change.

### Reconciliation (`pipeline-reconcile`)

Nothing guarantees an issue stays inside the label state machine, and two
failure modes accumulated undetected ([#246](https://github.com/derekwinters/lucas-doggiehood/issues/246)):
a merged PR left its issue `open` (so `v0.4` progress read 3/16 when it was
really 7/16), and a nightly build dropped an `in-progress` issue's commits so it
never re-entered the queue (invisible — not `ready-for-work`, not done, not
building; #109's stall blocked the whole #178 cascade). The reconciliation sweep
is the periodic check that catches this drift.

Detection is a **pure, unit-tested function** (`reconcile.py::process`), a
JSON-snapshot-in / findings-out shape exactly like `select_queue.py`; GitHub I/O
lives only at the edges. Each finding is classified **auto-fix** (a safe,
unambiguous label move applied by the gatekeeper) or **flag** (surfaced on the
dashboard for Derek):

| Rule | Condition | Action |
| - | - | - |
| **Closed + stale label** | `closed` issue still carrying any pipeline-state label (`ai-triage`, `pending-approval`, `needs-clarification`, `ready-for-work`, `in-progress`) | **auto-fix** — strip those labels (the `Closes #N` label-leak seen on #211) |
| **Stalled `in-progress`** | open, `in-progress`, no open PR, not on `main` | **auto-fix** — requeue `in-progress` → `ready-for-work` so the builder retries |
| **Merged-but-open** (incl. bundled squash) | open, work is on `main` | **flag** — surface in the dashboard "⚠️ Reconcile" section, *not* auto-closed |
| **Orphaned ready** (stretch) | open, `ready-for-work`, no milestone | **flag** |
| **Prose-only dependency** | open, body mentions `depends on #N` / `blocked by #N` in prose with no matching structured `Blocked by:` / `Depends on:` line for that number ([#248](https://github.com/derekwinters/lucas-doggiehood/issues/248), detected by `reconcile.prose_deps_in`) | **flag** |

The two auto-fixes are safe and unambiguous; everything about *closing* an issue
is either ambiguous or already owned by [#211](https://github.com/derekwinters/lucas-doggiehood/issues/211)
(auto-close-on-merge), so it is flagged, never applied. The sweep **never closes
an issue.** An open `in-progress` issue that is already on `main` classifies as
merged-but-open, never as a stall — that guard stops the #109 re-pick loop.

**Done-ness is decided by a merged commit *body* *closing-keyword* reference
(`Closes` / `Fixes` / `Resolves #N` and their tense/case variants) or
deliverables on `HEAD` — never a PR/commit *title*, and never a bare `#N` /
`Refs #N`.** This matches CLAUDE.md rule #10: only a closing keyword resolves an
issue; a bare `#N`, `Refs #N`, `Part of #N`, or `Relates to #N` merely links, so
a prose cross-reference in a merged commit body must not mark that issue done
([#277](https://github.com/derekwinters/lucas-doggiehood/issues/277)). The same
closing-keyword rule governs open-PR association (`has_open_pr`), so a PR that
only "Relates to #N" does not suppress that issue's stalled-`in-progress`
requeue. Titles are excluded separately: the nightly builder squash-merges
several issues under one lead PR title, so a title-only match keeps missing
bundled squashes (verified on #109/#58/#57/#190/#170). Both guards are locked in
unit tests.

The sweep runs in the **gatekeeper** step of each scheduled routine (after
command processing, so it reconciles against the labels those commands just
set), which applies the two auto-fixes; the **dashboard** render lists the flag
findings. Both share the one `reconcile.py` implementation.

### Dashboard (`pipeline-dashboard` + `dashboard.yml`)

Read-only. `render_dashboard.py` recomputes live state and rewrites **#193** in
place: a 7-slice focus-milestone pie — Done, Ready for work, In progress,
Needs triage, Pending approval, Needs clarification, Parked — one slice per
real pipeline-state label plus Done, so every focus-milestone issue maps to
exactly one slice and none (including a milestone-tagged `parked` issue) can
silently vanish from the total ([#265](https://github.com/derekwinters/lucas-doggiehood/issues/265)),
the focus ready-for-work queue (headed by the nightly build cap,
"Nightly build cap: **N**", read from the `<!-- pipeline-cap: N -->` marker —
[#240](https://github.com/derekwinters/lucas-doggiehood/issues/240)), "Your
move" counts, PRs (release-
please separated), intake, pending-approval and needs-clarification (each of
these three tables carries a **"Blocked by"** column listing the issue's
structured `Blocked by: #N` hard blockers as links, so blockers surface on every
stage the way the focus queue already flags them — [#241](https://github.com/derekwinters/lucas-doggiehood/issues/241)), a read-only
**"⏸️ Parked"** section listing every open `parked` issue so parked work stays
visible and easy to `/unpark` ([#249](https://github.com/derekwinters/lucas-doggiehood/issues/249)),
a **"⚠️ Reconcile"** section listing the sweep's flag findings (merged-but-open,
orphaned ready, prose-only dependencies — [#246](https://github.com/derekwinters/lucas-doggiehood/issues/246)),
other-milestone progress, and the command reference. In the focus ready-for-work
queue, **unblocking issues are starred** ([#250](https://github.com/derekwinters/lucas-doggiehood/issues/250)):
an issue that is open, not itself blocked by any open issue, and listed in at
least one other open issue's structured `Blocked by:` set is the highest-leverage
pick, so its row is marked `⭐ unblocks #57, #58, …` (the open issues it frees)
and it sorts to the top of the queue; blocked rows keep their `⛔ _blocked_` flag
and fully-independent rows stay unmarked. The unblocker set is derived from the
same structured `Blocked by:` graph the nightly builder and reconcile read — never
prose — by the pure `compute_unblockers` helper. It excludes #193 itself,
and keeps `parked` issues out of every *active work* queue and count
(ready-for-work queue, "Your move", intake, pending-approval, needs-clarification,
reconcile) — the Parked section is a separate listing, not a re-admission.
The one exception is the focus-milestone pie, which counts a milestone-tagged
`parked` issue in its own dedicated "Parked" slice ([#265](https://github.com/derekwinters/lucas-doggiehood/issues/265))
so it stays visible in the total instead of vanishing; the generic "Other
milestones" roll-up still excludes `parked` entirely, unchanged. Nothing here
mutates anything else. **Closed milestones** (100% done)
are omitted from the "Other milestones" section and the open-issues chart —
only live milestones outside the focus are shown ([#214](https://github.com/derekwinters/lucas-doggiehood/issues/214)).

## Skills

Each stage is a self-contained skill directory under `.claude/skills/`:

- `pipeline-gatekeeper/` — `SKILL.md` + `parse_commands.py` (deterministic
  command parser) + `check_revisits.py` (blocker auto-revisit transition) +
  tests.
- `pipeline-analysis/` — `SKILL.md` (model-driven triage/design).
- `pipeline-dashboard/` — `SKILL.md` + `render_dashboard.py` + golden test;
  driven in production by `.github/workflows/dashboard.yml`.
- `pipeline-dev/` — `SKILL.md` + `select_queue.py` (eligibility + topological
  ordering) + tests.
- `pipeline-reconcile/` — `SKILL.md` + `reconcile.py` (drift detection +
  auto-fix/flag classification) + tests; run in the gatekeeper step and
  surfaced by the dashboard render.

The deterministic parts (command parsing, queue selection, dashboard render,
reconciliation) are scripted and unit-tested (run in CI via
[`pipeline-tests.yml`](ci-cd.md)); the model does only analysis, development,
and light acknowledgments.
