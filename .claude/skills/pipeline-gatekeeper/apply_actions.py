#!/usr/bin/env python3
"""Apply layer for the comment-triggered gatekeeper workflow (issue #319).

Pure functions that turn one `parse_commands` action (or skip record) into
the concrete write instructions the workflow issues — label PATCH body,
acknowledgment comment text, reactions, and the one milestone write a
`/milestone` command carries. No GitHub I/O lives here; the workflow's glue
script (not unit-tested, like `reconcile.fetch_state`) performs the actual
PATCH/POST calls using what this module computes.

Label PATCH is a **merge**, computed here rather than trusted to the action
alone, because the GitHub REST API's label PATCH *replaces the whole set* —
there is no add/remove verb — so `add_labels`/`remove_labels` must be applied
against a freshly read-first snapshot of the issue's current labels.

No milestone is set on `/approve` anymore (Part A, #319): the milestone is
already on the issue by the time `/approve` fires (analysis set it at
`pending-approval`), so `milestone_write_for` only ever returns non-null for
an actual `/milestone` command, never for a bare `/approve`.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from parse_commands import MENUS  # noqa: E402

TRIAGE_LABEL = "ai-triage"


def fires_triage(current_labels, new_labels):
    """True when a label transition *newly* adds ``ai-triage``.

    The reactive-triage hook (#378): the instant the gatekeeper adds
    ``ai-triage`` to an issue, the workflow fires the analysis Routine for that
    issue (``fire_routine.fire``) so triage runs immediately instead of waiting
    for the next scheduled routine. Computed from the transition — the label
    set *before* vs *after* the PATCH — not from ``action["add_labels"]``
    alone, so a no-op re-add of a label the issue already carries never
    re-fires (idempotent re-runs, e.g. the cron missed-command net
    re-processing an already-admitted issue, stay silent).
    """
    return (TRIAGE_LABEL in set(new_labels)
            and TRIAGE_LABEL not in set(current_labels))


def merge_labels(current_labels, action):
    """Compute the full label list to PATCH.

    ``current_labels`` is the issue's labels *before* this action. Returns
    ``current_labels`` with ``action["remove_labels"]`` removed and
    ``action["add_labels"]`` appended (skipping any already present) — the
    full replacement set the PATCH-based labels endpoint requires.
    """
    remove = set(action.get("remove_labels") or [])
    add = action.get("add_labels") or []
    kept = [l for l in current_labels if l not in remove]
    for l in add:
        if l not in kept:
            kept.append(l)
    return kept


def render_ack(action):
    """Render the short "Your move" acknowledgment text for an action.

    Returns ``None`` when the action carries no menu (e.g. a bare `/focus` or
    `/cap` on the dashboard issue) — those are applied silently, with no ack
    comment, so the dashboard body isn't churned by acknowledgments.
    """
    menu = action.get("menu")
    if menu is None:
        return None
    return "Your move: %s" % MENUS[menu]


def reactions_for(action):  # noqa: ARG001 (action reserved for future use)
    """Reactions to add to the source comment.

    👍 (`+1`) confirms the action; 👀 (`eyes`) is the processed watermark that
    makes re-runs idempotent. Both always fire for any honored action.
    """
    return ["+1", "eyes"]


def milestone_write_for(action):
    """The milestone title to PATCH onto the issue, or ``None``.

    Only a `/milestone` command's own action carries this (Part A, #319): an
    `/approve` action's `set_milestone` is always null, since the milestone is
    already correct on the issue by the time `/approve` can fire.
    """
    return action.get("set_milestone")


def render_skip_ack(skip):
    """Render the hand-back reply for a refused command, or ``None``.

    Two refusals need Derek told why nothing moved:
      * `approve-no-milestone` (#247/#319) — the which-milestone hand-back.
      * `blocker-unscheduled` / `blocker-inversion` (#212) — the milestone-order
        refusal, whose full text (naming #A and #B and stating the fix) is
        composed by `parse_commands` and carried on the skip's `ack` field, so
        the apply layer posts it verbatim.
    Every other skip reason (`not-owner`, `parked-ignored`, `no-op`,
    `focus-no-match`, `milestone-no-match`, `cap-invalid`) gets no reply.
    """
    reason = skip.get("reason")
    if reason in ("blocker-unscheduled", "blocker-inversion"):
        return skip.get("ack")
    if reason != "approve-no-milestone":
        return None
    text = "Can't approve #%d to `ready-for-work` — no milestone resolved; reply" % skip["issue"]
    menu = skip.get("menu")
    if menu:
        text += " %s" % MENUS[menu]
    return text
