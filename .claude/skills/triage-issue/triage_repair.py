#!/usr/bin/env python3
"""Re-fire idempotency / partial-write recovery for reactive triage (#582).

The reactive-triage hand-off does two non-transactional GitHub writes: post the
analysis comment, then set the hand-back state label (removing `ai-triage`).
`triage-issue/SKILL.md` fixes the ORDERING (comment first, label second) so the
only residual partial-write shape is "analysis posted, label move never ran".

This module is the DETECTION side of the re-fire repair: when a triage run is
re-fired on such an issue, it must apply only the missing label move instead of
reposting a duplicate analysis. Kept as a pure, unit-tested function (the same
JSON-in / decision-out split as `pipeline-reconcile/reconcile.py`'s rules); the
signature recognizer is shared with reconcile so "what counts as an analysis
comment" has exactly one definition across the pipeline.
"""

import os
import sys

# Reuse the single canonical analysis-signature recognizer (#582) rather than
# re-defining it — same cross-skill import pattern `run_sweep.py` uses to share
# `reconcile`.
sys.path.insert(
    0, os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    os.pardir, "pipeline-reconcile"))
import reconcile  # noqa: E402  (imported after sys.path tweak)

# The hand-back state labels a completed triage sets. Their PRESENCE means the
# label move already ran, so there is nothing to repair.
HANDBACK_STATE_LABELS = ("pending-approval", "needs-clarification")


def analysis_comment_times(comments):
    """The ``created_at`` timestamps of the triage-authored analysis comments.

    Keeps exactly the comments whose body matches the triage analysis signature
    (``reconcile.has_analysis_signature`` — a ``## Build checklist`` heading, or
    the ``❓ Needs from Derek/Lucas`` marker inline *or* as a heading, #710),
    preserving input order. Each ``comments`` item is a dict with ``body`` and
    ``created_at`` keys.
    """
    return [c.get("created_at") for c in comments
            if reconcile.has_analysis_signature(c.get("body"))]


def is_partial_write_repair(labels, analysis_times, readmit_time):
    """True iff a re-fire should REPAIR a prior partial write rather than repost.

    A repair situation exists when a triage analysis for the CURRENT admission
    was already posted, but the hand-back label move never completed:

    * no hand-back state label is set yet (``pending-approval`` /
      ``needs-clarification`` — their presence means the move already ran), AND
    * at least one analysis comment was posted at or after ``readmit_time`` —
      the most recent re-admission signal (the latest ``ai-triage`` add or owner
      ``/revise`` / ``/redo``). An analysis that predates ``readmit_time`` is
      stale (e.g. superseded by a later ``/redo``) and must NOT be repaired — the
      re-fire re-triages fresh.

    Timestamps are ISO-8601 UTC (``...Z``) strings, whose lexical order matches
    chronological order; ``None`` entries are ignored. On True, the re-fire
    applies only the missing label move; on False it triages normally.
    """
    if any(l in labels for l in HANDBACK_STATE_LABELS):
        return False
    return any(t is not None and t >= readmit_time
               for t in (analysis_times or []))
