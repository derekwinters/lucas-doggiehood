# Agentic Delivery Pipeline — pitch documents

Standalone HTML pages documenting the AI issue-management pipeline and pitching its enterprise
form. Everything opens directly in a browser (double-click — no webserver, no network, no
external assets) and prints to PDF.

## Set A — grounded in this repository (documents 1–3)

These reference this project by name and map every claim to its source files. Use them to
verify accuracy and to show the working reference implementation.

| # | File | Audience | Contents |
|---|------|----------|----------|
| 1 | [`01-executive-overview.html`](01-executive-overview.html) | Leadership | The operating loop at a glance, the three-layer trust model (humans decide / AI executes / scripts enforce), and what the model buys. |
| 2 | [`02-current-pipeline.html`](02-current-pipeline.html) | Engineers (and verification) | The as-built pipeline in this repo: full issue lifecycle with every gating section, the skills/agents matrix by phase, the daily schedule, the command vocabulary, the TDD build loop, safety rails, and a source-file map for auditing every claim. |
| 3 | [`03-work-blueprint.html`](03-work-blueprint.html) | Leadership + engineers | **Proposal.** The same model re-based onto Azure DevOps: Epic → Feature → Story → Task, the New/Research/Ready/Develop/Done/Closed state flow as the gating sections, quarter/sprint cadence, the `grill-*` skills, generic TDD agents, multi-team/multi-repo federation, engineer FAQ, and a rollout plan. |

## Set B — fully decoupled, work-ready (documents 4–6)

Zero references to this project or any personal context. Written as a pipeline already in
production at work (Azure DevOps + GitHub, grill skills, generic TDD agents), pitched for
adoption by more teams. Safe to present or share as-is. The three files tell the same story in
three formats — pick per audience and setting.

| # | File | Format | Notes |
|---|------|--------|-------|
| 4 | [`04-work-pitch.html`](04-work-pitch.html) | Written document | The full pitch: lifecycle, trust model, hierarchy, skills-by-phase, daily rhythm, commands, federation, metrics, one-config-PR onboarding, engineer FAQ. Simplified diagrams. |
| 5 | [`05-deck.html`](05-deck.html) | Slide deck (15 slides) | Keyboard-driven: → / space advance (with staged reveals), ← back, F fullscreen, click/tap edges or swipe. Auto-scales to any screen; print for a per-slide PDF. |
| 6 | [`06-scrolly.html`](06-scrolly.html) | Scroll story | Four acts with a sticky animated pipeline: stages light up as you scroll, a day-cycle marker moves across the timeline, federation tiers build in. Degrades gracefully without JS and in print. |

## Conventions used in the diagrams

- **Large boxes** are gating sections — where a work item sits while waiting or being worked.
- **Small boxes (chips)** are facts about the item in that section: its state/label, the skills
  and scripts acting on it, and its exit conditions. Chips are labels only — no sentences;
  explanatory prose lives in the text around each diagram.
- **Pills on connectors** are the gates that move an item forward, color-coded by who acts:
  amber = human decision, purple = AI (skill/agent), teal = deterministic script or workflow.
- Colors carry the same meaning on every page; each page's legend shows the subset of keys that
  page actually uses.

## Status

- Document 2 is descriptive and is intended to match this repository exactly; its final section
  maps each claim to the source file that backs it.
- Documents 3–6 are proposal material. Set B is presentation-ready and intentionally contains
  no trace of this repository.
