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
    """Render the which-milestone hand-back for an `approve-no-milestone` skip.

    Every other skip reason (`not-owner`, `parked-ignored`, `no-op`,
    `focus-no-match`, `milestone-no-match`, `cap-invalid`) gets no reply
    comment — only a refused `/approve` needs Derek told why nothing moved.
    """
    if skip.get("reason") != "approve-no-milestone":
        return None
    text = "Can't approve #%d to `ready-for-work` — no milestone resolved; reply" % skip["issue"]
    menu = skip.get("menu")
    if menu:
        text += " %s" % MENUS[menu]
    return text
