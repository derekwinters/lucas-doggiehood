---
name: pipeline-analysis
description: >
  Triage and (where authorized) design admitted issues for the Doggiehood
  pipeline. For every `ai-triage` issue: diagnose bugs, plan spec-covered
  features with a proposed milestone, or stop and ask when a new design/
  wireframe decision is needed — never inventing design. Routes each issue to
  await Derek. Runs in the AM triage routine after the gatekeeper.
---

# Pipeline analysis

Digs into every `ai-triage` issue, in parallel, and routes it to await Derek.
Runs in the **AM triage** routine (7 AM CT), after the gatekeeper. See
`docs/engineering/issue-pipeline.md` and honor the project's hard rules in
`CLAUDE.md` — especially **rule #8 (wireframe before UI)** and the
**no-inventing-design** rule.

## The one rule that overrides everything

**Never invent a design decision, mechanic, quest type, breed, or UI layout.**
If a feature needs a call that isn't already settled in `docs/specs/`, or would
touch a UI screen's structure with no approved wireframe in `docs/specs/ui/`,
you **stop and ask** — you do not draft it. The only exception is an issue
carrying an explicit `/propose` from Derek (see below).

## Scope

- Act only on **open** issues labeled `ai-triage`.
- **Never** touch `type:epic`, the `dashboard` issue (#193), or any `parked`
  issue.
- Fan out across the admitted issues in **parallel** with bounded concurrency
  (a handful at a time), one sub-agent per issue. Each issue is independent.

## Routing — hybrid by kind

For each `ai-triage` issue, first read the issue, its comments (including any
`/revise` notes or `/propose` from Derek), and the `/docs` pages it relates to.
Then route:

1. **Bug** → root-cause **diagnosis** + a recommended fix approach, ending with
   a **`## Build checklist`** (acceptance criteria — see below). Add
   `type:bug`. Post the analysis, set `pending-approval`.

2. **Feature fully covered by the specs** → a concrete **implementation plan**
   grounded in the relevant `docs/specs/` pages, a **proposed milestone**
   (see milestone matching), and a closing **`## Build checklist`** (acceptance
   criteria — see below). Post it, set `pending-approval`.

3. **Feature needing a new design decision or a UI wireframe (rule #8)** →
   **stop and ask.** Post a clearly-labeled block:

   ```
   ❓ Needs from Derek/Lucas: <one specific, self-contained question,
   stating the options and what each would mean>
   ```

   Set `needs-clarification`. The question must stand on its own — someone
   reading only that block should understand the decision. Never proceed to a
   plan **or a Build checklist** for this kind — it stops at the question.

4. **`/propose` present on the issue** (an owner comment containing `/propose`)
   → you are authorized to draft the missing wireframe/mechanic, but only as a
   clearly-marked **PROPOSAL** (prefix the section `PROPOSAL (draft for your
   approval):`), ending with a **`## Build checklist`** (acceptance criteria —
   see below). Then set `pending-approval`. This is the single opt-in that
   lets analysis suggest design; without it, case 3 applies.

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

## Milestone matching — read milestones live

Propose the milestone by matching the issue against the **live milestone
descriptions** from the milestones API — never a hard-coded `00`–`08` list, so
this survives the version-numbering rework (#192). Pick the milestone whose
description best fits the work; state which one and why in the analysis. Derek's
`/approve` accepts it; `/milestone` overrides it.

## Dependencies — first-class GitHub relationships

Record dependencies as real relationships, not just prose:

- **Decomposition** (an issue is really several) → create **sub-issues** and
  link them as children.
- **Peer dependency** (issue A can't start until issue B merges) → add a
  `Blocked by: #B` line to A's body (the dashboard and `pipeline-dev` parse
  this), and use native issue-dependencies if writable.
- **Likely duplicate** → link the candidate issue and note it; don't close
  anything — flag it for Derek.

Distinguish a **hard blocker** (`Blocked by:` — gates eligibility for dev) from
a **soft ordering** hint between sibling sub-issues (note it as `Depends on: #N`
so dev orders them without blocking).

## Every hand-back ends with a menu

Close each comment with the context-appropriate "Your move" line:

- `pending-approval` (bug or plan) → `/approve` · `/revise <notes>` · `/redo` ·
  `/park`
- `needs-clarification` → answer inline, or `/revise <notes>` · `/redo` ·
  `/propose` · `/park`

## After analysis

Do **not** move issues to `ready-for-work` yourself — only the gatekeeper does
that on Derek's `/approve`. Report a one-line summary per issue analyzed
(number → routed-to state + proposed milestone), and flag anything you had to
stop on.
