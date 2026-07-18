# Agentic Delivery Pipeline — pitch documents

Three standalone HTML pages that document the AI issue-management pipeline running in this
repository and propose its translation to an enterprise Azure DevOps + GitHub environment.
Open any of them directly in a browser (double-click — no webserver, no network, no external
assets) or print them to PDF. They cross-link to each other, so keep the three files together.

| # | File | Audience | Contents |
|---|------|----------|----------|
| 1 | [`01-executive-overview.html`](01-executive-overview.html) | Leadership | The operating loop at a glance, the three-layer trust model (humans decide / AI executes / scripts enforce), and what the model buys. |
| 2 | [`02-current-pipeline.html`](02-current-pipeline.html) | Engineers (and verification) | The as-built pipeline in this repo: full issue lifecycle with every gating section, the skills/agents matrix by phase, the daily schedule, the command vocabulary, the TDD build loop, safety rails, and a source-file map for auditing every claim. |
| 3 | [`03-work-blueprint.html`](03-work-blueprint.html) | Leadership + engineers | **Proposal.** The same model re-based onto Azure DevOps: Epic → Feature → Story → Task, the New/Research/Ready/Develop/Done/Closed state flow as the gating sections, quarter/sprint cadence, the `grill-*` skills, generic TDD agents, multi-team/multi-repo federation, engineer FAQ, and a rollout plan. |

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
- Document 3 is a proposal draft, written to be revised once Document 2 is verified.
