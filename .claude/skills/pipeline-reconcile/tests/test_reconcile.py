"""Tests for the pipeline-reconcile detection rules.

Run: python3 -m unittest discover -s .claude/skills/pipeline-reconcile/tests
Pure JSON-in/JSON-out; no GitHub access needed. See ../reconcile.py.

Each rule is exercised with a representative fixture plus its healthy/negative
counterpart, so a regression in the classification split (done-ness vs. stall)
fails CI. The bundled-squash blind spot (issue #246, 2026-07-23 comment) is
locked in by `test_title_only_reference_does_not_flag_done`.
"""

import json
import os
import subprocess
import sys
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), os.pardir, "reconcile.py")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import reconcile  # noqa: E402  (imported after sys.path tweak)


def run(payload):
    proc = subprocess.run(
        [sys.executable, SCRIPT],
        input=json.dumps(payload),
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0, proc.stderr
    return json.loads(proc.stdout)


def issue(number, **kw):
    d = {
        "number": number,
        "state": "open",
        "labels": [],
        "milestone": "v0.4",
        "is_epic": False,
        "is_dashboard": False,
        "has_open_pr": False,
        "prose_deps": [],
        "has_analysis_comment": False,
    }
    d.update(kw)
    return d


def payload(issues, merged_commit_body_refs=None, deliverables_present=None):
    return {
        "issues": issues,
        "merged_commit_body_refs": merged_commit_body_refs or [],
        "deliverables_present": deliverables_present or {},
    }


def numbers(findings):
    return [f["number"] for f in findings]


class TestClosedStaleLabels(unittest.TestCase):
    def test_closed_with_stale_labels_strips_exactly_those(self):
        # A closed issue still carrying pipeline-state labels -> strip only the
        # pipeline-state labels (mirror of merged-but-open; the #211 Closes gap).
        out = run(payload([
            issue(211, state="closed",
                  labels=["in-progress", "type:task", "area:ai"]),
        ]))
        self.assertEqual(numbers(out["strip_labels"]), [211])
        self.assertEqual(out["strip_labels"][0]["labels"], ["in-progress"])

    def test_closed_with_multiple_stale_labels(self):
        out = run(payload([
            issue(500, state="closed",
                  labels=["ready-for-work", "in-progress", "ai-triage"]),
        ]))
        # Reported in the canonical pipeline-state order, not input order.
        self.assertEqual(
            out["strip_labels"][0]["labels"],
            ["ai-triage", "ready-for-work", "in-progress"],
        )

    def test_clean_closed_issue_no_finding(self):
        out = run(payload([
            issue(400, state="closed", labels=["type:task", "area:ai"]),
        ]))
        self.assertEqual(out["strip_labels"], [])

    def test_open_issue_never_strip(self):
        # strip_labels only ever fires on CLOSED issues.
        out = run(payload([issue(300, state="open", labels=["in-progress"])]))
        self.assertEqual(out["strip_labels"], [])


class TestStalledInProgress(unittest.TestCase):
    def test_stalled_in_progress_requeues(self):
        # open, in-progress, no open PR, not on main -> return to ready-for-work.
        out = run(payload([issue(109, labels=["in-progress"])]))
        self.assertEqual(numbers(out["requeue"]), [109])
        self.assertEqual(out["requeue"][0]["from"], "in-progress")
        self.assertEqual(out["requeue"][0]["to"], "ready-for-work")

    def test_in_progress_with_open_pr_not_requeued(self):
        out = run(payload([issue(109, labels=["in-progress"], has_open_pr=True)]))
        self.assertEqual(out["requeue"], [])

    def test_ready_for_work_not_requeued(self):
        out = run(payload([issue(109, labels=["ready-for-work"])]))
        self.assertEqual(out["requeue"], [])


class TestEventsOnlyMode(unittest.TestCase):
    """`process(data, events_only=...)` (issue #319): the event-triggered sweep
    workflow (`issues: [closed, labeled]` / `pull_request: [closed]`) must NOT
    emit `requeue` — only the cron backstop does. Running `requeue` on an event
    can race GitHub's own `Closes #N` auto-close: right at merge, a just-merged
    `in-progress` issue can transiently look like a stalled `in-progress` (PR no
    longer open, `main` not yet showing the merge), and requeuing it there would
    re-arm the #109 re-pick loop. `strip_labels` and `flag_done` are unaffected
    in either mode — they can't fire early (strip only ever touches an
    already-closed issue) or aren't a write at all (flag is read-only).
    """

    def test_events_only_omits_requeue(self):
        out = reconcile.process(
            payload([issue(109, labels=["in-progress"])]), events_only=True)
        self.assertEqual(out["requeue"], [])

    def test_cron_mode_still_requeues(self):
        out = reconcile.process(
            payload([issue(109, labels=["in-progress"])]), events_only=False)
        self.assertEqual(numbers(out["requeue"]), [109])

    def test_default_mode_is_cron_shaped(self):
        # No events_only argument at all -> full behavior (existing callers,
        # e.g. the cron path and every other test in this file, are unaffected).
        out = reconcile.process(payload([issue(109, labels=["in-progress"])]))
        self.assertEqual(numbers(out["requeue"]), [109])

    def test_events_only_keeps_strip_labels_and_flag_done(self):
        out = reconcile.process(payload(
            [issue(211, state="closed", labels=["in-progress"]),
             issue(56, labels=["in-progress"])],
            merged_commit_body_refs=[56],
        ), events_only=True)
        self.assertEqual(numbers(out["strip_labels"]), [211])
        self.assertEqual(numbers(out["flag_done"]), [56])

    def test_cli_events_only_flag_omits_requeue(self):
        # The sweep workflow's event path invokes the CLI with --events-only
        # (see gatekeeper-sweep.yml); the cron path omits the flag.
        proc = subprocess.run(
            [sys.executable, SCRIPT, "--events-only"],
            input=json.dumps(payload([issue(109, labels=["in-progress"])])),
            capture_output=True, text=True,
        )
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertEqual(json.loads(proc.stdout)["requeue"], [])

    def test_cli_without_flag_still_requeues(self):
        proc = subprocess.run(
            [sys.executable, SCRIPT],
            input=json.dumps(payload([issue(109, labels=["in-progress"])])),
            capture_output=True, text=True,
        )
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertEqual(numbers(json.loads(proc.stdout)["requeue"]), [109])


class TestMergedButOpen(unittest.TestCase):
    def test_merged_but_open_via_commit_body_flags(self):
        # #56 landed on main (its number is in a merged commit body) but is
        # still open -> flag_done for Derek to close (not auto-closed; #211
        # owns the clean single-issue auto-close).
        out = run(payload(
            [issue(56, labels=["in-progress"])],
            merged_commit_body_refs=[56],
        ))
        self.assertEqual(numbers(out["flag_done"]), [56])

    def test_title_only_reference_does_not_flag_done(self):
        # The bundled-squash guard (#246): done-ness is decided ONLY by a
        # merged commit *body* reference (or deliverables), never by a PR/commit
        # *title* referencing the issue. #999 is referenced only by a title, so
        # it is NOT in merged_commit_body_refs -> no flag_done.
        out = run(payload(
            [issue(999, labels=["in-progress"])],
            merged_commit_body_refs=[],
        ))
        self.assertEqual(out["flag_done"], [])
        # ...and with no on-main signal it is instead treated as a stall.
        self.assertEqual(numbers(out["requeue"]), [999])

    def test_deliverables_present_flags_done(self):
        # The second done-ness signal: the issue's deliverables exist at HEAD.
        out = run(payload(
            [issue(58, labels=["in-progress"])],
            deliverables_present={"58": True},
        ))
        self.assertEqual(numbers(out["flag_done"]), [58])

    def test_closed_done_issue_not_flag_done(self):
        # flag_done is for OPEN issues; a closed one is handled by strip_labels.
        out = run(payload(
            [issue(56, state="closed", labels=["in-progress"])],
            merged_commit_body_refs=[56],
        ))
        self.assertEqual(out["flag_done"], [])
        self.assertEqual(numbers(out["strip_labels"]), [56])


class TestClassificationSplit(unittest.TestCase):
    def test_done_in_progress_flags_not_requeue(self):
        # The done-ness guard that stops the #109 re-pick loop: an open
        # in-progress issue that IS already on main classifies as flag_done,
        # never requeue.
        out = run(payload(
            [issue(109, labels=["in-progress"])],
            merged_commit_body_refs=[109],
        ))
        self.assertEqual(numbers(out["flag_done"]), [109])
        self.assertEqual(out["requeue"], [])


class TestStretchRules(unittest.TestCase):
    def test_orphaned_ready_no_milestone_flags(self):
        out = run(payload([issue(300, labels=["ready-for-work"], milestone=None)]))
        self.assertEqual(numbers(out["flag_orphaned_ready"]), [300])

    def test_ready_with_milestone_not_orphaned(self):
        out = run(payload([issue(300, labels=["ready-for-work"], milestone="v0.4")]))
        self.assertEqual(out["flag_orphaned_ready"], [])

    def test_prose_only_dependency_flags(self):
        out = run(payload([issue(178, labels=["ready-for-work"], prose_deps=[109])]))
        self.assertEqual(numbers(out["flag_prose_dep"]), [178])
        self.assertEqual(out["flag_prose_dep"][0]["refs"], [109])


class TestClosingRefsParsing(unittest.TestCase):
    """Parsing layer (#277): done-ness / open-PR association counts ONLY closing
    keywords (Closes/Fixes/Resolves #N + tense/case variants), per CLAUDE.md
    rule #11. A bare `#N`, `Refs #N`, `Part of #N`, `Relates to #N` only links.
    """

    def test_prose_and_bare_refs_are_not_closing(self):
        # None of these landed work per rule #11 -> empty closing-ref set.
        for text in [
            "Relates to #250",
            "Part of #191",
            "Refs #57",
            "follow-up to #244",
            "Follow-up to #244",
            "see #58 for context",
            "#58",
            "Reverts #58",
        ]:
            self.assertEqual(reconcile._closing_refs_in(text), set(), text)

    def test_closing_keywords_capture_number(self):
        self.assertEqual(reconcile._closing_refs_in("Closes #58"), {58})
        self.assertEqual(reconcile._closing_refs_in("Fixes #58"), {58})
        self.assertEqual(reconcile._closing_refs_in("Resolves #58"), {58})

    def test_case_and_tense_variants_all_close(self):
        for text in [
            "closes #58",
            "close #58",
            "CLOSED #58",
            "fix #58",
            "FIXED #58",
            "Fixes #58",
            "resolve #58",
            "Resolved #58",
            "RESOLVES #58",
        ]:
            self.assertEqual(reconcile._closing_refs_in(text), {58}, text)

    def test_colon_separator_allowed(self):
        self.assertEqual(reconcile._closing_refs_in("Closes: #58"), {58})

    def test_mixed_prose_and_closing_keeps_only_closing(self):
        text = "Relates to #250\n\nFixes #58\nPart of #191\nRefs #99"
        self.assertEqual(reconcile._closing_refs_in(text), {58})

    def test_multiple_closing_refs(self):
        self.assertEqual(
            reconcile._closing_refs_in("Closes #10, fixes #20\nResolves #30"),
            {10, 20, 30},
        )

    def test_word_boundary_prefix_not_matched(self):
        # 'prefix #5' must not trip the 'fix' keyword.
        self.assertEqual(reconcile._closing_refs_in("prefix #5"), set())

    def test_empty_and_none(self):
        self.assertEqual(reconcile._closing_refs_in(""), set())
        self.assertEqual(reconcile._closing_refs_in(None), set())


class TestProseDepDetection(unittest.TestCase):
    """`prose_deps_in(body)` (issue #248): flag a dependency written in prose
    (`depends on #N` / `blocked by #N`, case-insensitive) that has NO matching
    structured `Blocked by: #N` / `Depends on: #N` line for that number. The
    colon-bearing line is the canonical "structured" form; its presence for a
    given number always clears it. A bare `#N` that is not a dependency phrase
    is never flagged.
    """

    # --- positive: prose-only references are flagged ----------------------
    def test_prose_depends_on_without_structured_line_flagged(self):
        self.assertEqual(
            reconcile.prose_deps_in("This work depends on #109 landing first."),
            [109],
        )

    def test_prose_blocked_by_without_structured_line_flagged(self):
        self.assertEqual(
            reconcile.prose_deps_in("It is blocked by #57 until that ships."),
            [57],
        )

    def test_case_insensitive_phrase(self):
        for text in ["Depends On #109", "DEPENDS ON #109", "Blocked By #109",
                     "blocked by #109"]:
            self.assertEqual(reconcile.prose_deps_in(text), [109], text)

    def test_multiple_numbers_on_one_prose_line(self):
        # #57's real body: "Depends on #109 … and #56" — both are prose-only.
        self.assertEqual(
            reconcile.prose_deps_in("Depends on #109 and also #56 for economy"),
            [56, 109],
        )

    def test_no_colon_line_start_is_prose_not_structured(self):
        # Line begins with the keyword but has NO colon -> still prose, flagged.
        self.assertEqual(reconcile.prose_deps_in("Blocked by #57"), [57])

    # --- negative: a matching structured line clears the number -----------
    def test_structured_line_present_not_flagged(self):
        body = "Depends on: #109\n\nThe economy work depends on #109 landing."
        self.assertEqual(reconcile.prose_deps_in(body), [])

    def test_structured_line_only_clears_its_own_number(self):
        body = "Depends on: #109\nAlso it is blocked by #57 in a sentence."
        self.assertEqual(reconcile.prose_deps_in(body), [57])

    def test_structured_line_with_list_marker_and_extra_text(self):
        body = "- Blocked by: #57 (must merge first)\nblocked by #57 too"
        self.assertEqual(reconcile.prose_deps_in(body), [])

    def test_structured_line_multiple_numbers(self):
        body = "Depends on: #109, #110\nWork depends on #109 and #110."
        self.assertEqual(reconcile.prose_deps_in(body), [])

    def test_native_relationship_clears_number(self):
        # A number carried as a native GitHub relationship also satisfies.
        self.assertEqual(
            reconcile.prose_deps_in("depends on #109", native_refs=[109]),
            [],
        )

    def test_native_refs_clear_only_their_own_number(self):
        # A native relationship for #109 clears that number, but a different
        # prose-only dependency (#57) with no native/structured backing stays
        # flagged — parity with the structured-line behavior. This is the
        # `native_refs` fetch path `fetch_state` wires in for #321: a native
        # hard-blocker relationship is as good as a `Blocked by:` line.
        self.assertEqual(
            reconcile.prose_deps_in(
                "depends on #109 and it is blocked by #57", native_refs=[109]),
            [57],
        )

    def test_native_refs_multiple_numbers_cleared(self):
        self.assertEqual(
            reconcile.prose_deps_in(
                "blocked by #57 and depends on #109", native_refs=[57, 109]),
            [],
        )

    # --- negative: non-dependency mentions are never flagged --------------
    def test_bare_hash_not_flagged(self):
        for text in ["See #58 for context", "Part of #191", "Relates to #250",
                     "Refs #57", "Follow-up to #244", "#58"]:
            self.assertEqual(reconcile.prose_deps_in(text), [], text)

    def test_no_body_or_empty(self):
        self.assertEqual(reconcile.prose_deps_in(""), [])
        self.assertEqual(reconcile.prose_deps_in(None), [])

    def test_word_boundary_unblocked_not_matched(self):
        # "unblocked by #5" is not "blocked by" a dependency.
        self.assertEqual(reconcile.prose_deps_in("It was unblocked by #5"), [])

    def test_result_sorted_and_deduped(self):
        body = "depends on #30 and #10, blocked by #10 and #20"
        self.assertEqual(reconcile.prose_deps_in(body), [10, 20, 30])


class TestExclusionsAndHealth(unittest.TestCase):
    def test_epic_excluded(self):
        out = run(payload([
            issue(191, state="closed", labels=["in-progress"], is_epic=True),
        ]))
        self.assertEqual(out["strip_labels"], [])

    def test_dashboard_excluded(self):
        out = run(payload([
            issue(193, labels=["in-progress"], is_dashboard=True),
        ]))
        self.assertEqual(out["requeue"], [])

    def test_parked_excluded(self):
        out = run(payload([
            issue(700, labels=["in-progress", "parked"]),
        ]))
        self.assertEqual(out["requeue"], [])

    def test_empty_and_healthy_board_no_findings(self):
        out = run(payload([
            issue(1, state="closed", labels=["type:task"]),
            issue(2, labels=["ready-for-work"], milestone="v0.4"),
            issue(3, labels=["in-progress"], has_open_pr=True),
        ]))
        for key in ("strip_labels", "requeue", "requeue_triage", "flag_done",
                    "flag_orphaned_ready", "flag_prose_dep",
                    "flag_orphaned_analysis"):
            self.assertEqual(out[key], [], key)

    def test_findings_sorted_by_number(self):
        out = run(payload([
            issue(300, labels=["in-progress"]),
            issue(100, labels=["in-progress"]),
            issue(200, labels=["in-progress"]),
        ]))
        self.assertEqual(numbers(out["requeue"]), [100, 200, 300])


class TestMergeBlockers(unittest.TestCase):
    """`merge_blockers(text_line, native)` (issue #321): the single canonical
    merge rule every pipeline reader shares — the text-line `Blocked by: #N`
    hard blockers UNIONED with the native GitHub issue-dependency blockers,
    de-duplicated and sorted. Keeping one helper means the blocker graph the
    nightly builder, reconcile, the dashboard, and the #212 milestone-order
    gate consume is identical everywhere.
    """

    def test_text_only(self):
        self.assertEqual(reconcile.merge_blockers([57, 109], []), [57, 109])

    def test_native_only(self):
        self.assertEqual(reconcile.merge_blockers([], [312]), [312])

    def test_union_deduped_and_sorted(self):
        # Overlap (#109 in both) collapses; result is sorted ascending.
        self.assertEqual(
            reconcile.merge_blockers([109, 57], [312, 109]),
            [57, 109, 312],
        )

    def test_empty_both(self):
        self.assertEqual(reconcile.merge_blockers([], []), [])

    def test_default_native_arg(self):
        # Migration-safe: a caller that only has the text line still works.
        self.assertEqual(reconcile.merge_blockers([57]), [57])

    def test_accepts_sets_and_strings(self):
        # Numbers may arrive as a set (native fetch) or numeric strings.
        self.assertEqual(reconcile.merge_blockers({"57"}, ["109"]), [57, 109])


# Verbatim body of the real flapping analysis comment on issue #100
# (https://github.com/derekwinters/lucas-doggiehood/issues/100#issuecomment-5213711064,
# 2026-08-07T07:07:23Z) — the regression fixture for issue #654. Its ONLY
# analysis signature is the BOLDED `❓ **Needs from Derek/Lucas:**` marker: the
# needs-clarification route never emits a `## Build checklist` heading, so a
# recognizer that misses the emphasis sees no analysis at all and `requeue_triage`
# re-fires triage forever.
ISSUE_100_FLAPPING_ANALYSIS = """\
**Re-triage — no new signal since the [2026-08-07 02:24 UTC answer](https://github.com/derekwinters/lucas-doggiehood/issues/100#issuecomment-5211379668).**

Same conclusion as the last three passes: this needs the actual quest dialogue wording from Derek/Lucas for `Assets/Scripts/Core/Quests/QuestTemplates.cs` — a voice/personality call nothing in `docs/specs` settles, and out of agent scope per `CLAUDE.md` ("don't invent personalities... not in the specs"). No `/revise`, `/redo`, or `/propose` note, no pasted lines since the last analysis, so not re-explaining the full breakdown again here (see the [2026-07-31 analysis](https://github.com/derekwinters/lucas-doggiehood/issues/100#issuecomment-5148444483) for the complete what-exists-vs-what's-needed rundown).

This pass exists because the issue landed back in bare `ai-triage` again despite the last three re-triages reaching the same conclusion — the `needs-clarification` hand-off has repeatedly failed to stick. Applying it now, removing `ai-triage` in the same write, so this stops re-queuing every cycle.

❓ **Needs from Derek/Lucas:** the quest dialogue lines for `Assets/Scripts/Core/Quests/QuestTemplates.cs`. Deliver via: edit the file directly and push/PR, paste replacement lines in a comment, or `/propose` for a draft to react to.

No milestone change — `Direct Involvement Needed` still fits.

Your move: answer inline (paste the lines) · `/revise <notes>` · `/redo` · `/propose` · `/park`

---
_Generated by [Claude Code](https://claude.ai/code)_"""


class TestAnalysisSignature(unittest.TestCase):
    """`has_analysis_signature(text)` (issue #582): recognize a triage-authored
    analysis comment the same way `_closing_refs_in` recognizes closing refs — a
    `## Build checklist` heading (any level) OR the `❓ Needs from Derek/Lucas:`
    marker, the two hand-back shapes `triage-issue/SKILL.md` defines. Pure
    text-in / bool-out; no GitHub access.

    The marker match tolerates Markdown emphasis runs around the marker text
    (issue #654) — triage's real comments bold it as `❓ **Needs from
    Derek/Lucas:**`, and a literal-substring recognizer missed every one of them.
    """

    def test_build_checklist_heading_matches(self):
        self.assertTrue(reconcile.has_analysis_signature("## Build checklist"))

    def test_build_checklist_heading_level_tolerant(self):
        self.assertTrue(reconcile.has_analysis_signature("### Build checklist"))
        self.assertTrue(reconcile.has_analysis_signature("# Build checklist"))

    def test_build_checklist_case_insensitive(self):
        self.assertTrue(reconcile.has_analysis_signature("## build checklist"))

    def test_needs_marker_matches(self):
        self.assertTrue(reconcile.has_analysis_signature(
            "❓ Needs from Derek/Lucas: which layout should the panel use?"))

    def test_full_analysis_body_matches(self):
        body = ("## Diagnosis\n\nRoot cause is X.\n\n## Fix approach\n\n"
                "Do Y.\n\n## Build checklist\n\n- [ ] Core test: ...\n")
        self.assertTrue(reconcile.has_analysis_signature(body))

    # --- Markdown emphasis around the marker (issue #654) ------------------
    def test_bolded_needs_marker_matches(self):
        # The shape triage actually writes — `**` sits between `❓ ` and `Needs`,
        # which a literal-substring recognizer never saw.
        self.assertTrue(reconcile.has_analysis_signature(
            "❓ **Needs from Derek/Lucas:** the actual dialogue lines."))

    def test_emphasis_variants_match(self):
        for text in [
            "❓ *Needs from Derek/Lucas:* which layout?",       # italic, colon in
            "❓ *Needs from Derek/Lucas*: which layout?",       # italic, colon out
            "❓ __Needs from Derek/Lucas:__ which layout?",     # bold underscores
            "❓ _Needs from Derek/Lucas_: which layout?",       # italic underscore
            "❓ ***Needs from Derek/Lucas:*** which layout?",   # bold+italic
            "❓**Needs from Derek/Lucas:** no space after the emoji",
            "**❓ Needs from Derek/Lucas:** emphasis outside the ❓",
        ]:
            self.assertTrue(reconcile.has_analysis_signature(text), text)

    def test_issue_100_flapping_comment_matches(self):
        # Regression fixture: the real #100 body whose only signature is the
        # bolded marker (no `## Build checklist` — the ask route never emits one).
        self.assertNotIn("Build checklist", ISSUE_100_FLAPPING_ANALYSIS)
        self.assertTrue(
            reconcile.has_analysis_signature(ISSUE_100_FLAPPING_ANALYSIS))

    def test_marker_still_requires_the_emoji(self):
        # Widening for emphasis must not drop the `❓` anchor: the bare phrase in
        # prose (or in a heading about the marker) is not a hand-back signature.
        for text in ["Needs from Derek/Lucas: nothing, this is prose.",
                     "**Needs from Derek/Lucas:** discussed offline already."]:
            self.assertFalse(reconcile.has_analysis_signature(text), text)

    def test_marker_does_not_span_unrelated_text(self):
        # The emphasis tolerance must only span emphasis/whitespace — not an
        # arbitrary run of prose between a stray `❓` and a later mention.
        text = ("❓ Should the panel scroll? I asked already.\n\n"
                "Nobody said this Needs from Derek/Lucas: it was answered.")
        self.assertFalse(reconcile.has_analysis_signature(text))

    def test_plain_comment_does_not_match(self):
        for text in ["/approve", "Your move: `/park`",
                     "I built a checklist for this", "Build checklist",
                     "See the checklist above", "LGTM"]:
            self.assertFalse(reconcile.has_analysis_signature(text), text)

    def test_empty_and_none(self):
        self.assertFalse(reconcile.has_analysis_signature(""))
        self.assertFalse(reconcile.has_analysis_signature(None))


class TestTriageHandoffDrift(unittest.TestCase):
    """Non-atomic triage hand-off drift (issue #582). Rule (a): an open issue in
    `pending-approval`/`needs-clarification` with NO analysis comment (the #569
    shape) auto-requeues to `ai-triage`. Rule (b): an open issue carrying an
    analysis comment but NO pipeline-state label (the residual #570 shape) is
    FLAGGED, not auto-fixed (intended hand-back state is ambiguous).
    """

    # --- rule (a): label-without-analysis -> requeue_triage (auto-fix) -----
    def test_pending_approval_without_analysis_requeues_to_triage(self):
        out = run(payload([
            issue(569, labels=["pending-approval"], has_analysis_comment=False),
        ]))
        self.assertEqual(numbers(out["requeue_triage"]), [569])
        self.assertEqual(out["requeue_triage"][0]["from"], "pending-approval")
        self.assertEqual(out["requeue_triage"][0]["to"], "ai-triage")

    def test_needs_clarification_without_analysis_requeues_to_triage(self):
        out = run(payload([
            issue(569, labels=["needs-clarification"],
                  has_analysis_comment=False),
        ]))
        self.assertEqual(numbers(out["requeue_triage"]), [569])
        self.assertEqual(out["requeue_triage"][0]["from"], "needs-clarification")

    def test_pending_approval_with_analysis_not_requeued(self):
        # Healthy triaged issue: has both its analysis comment and its state
        # label -> neither new rule fires.
        out = run(payload([
            issue(569, labels=["pending-approval"], has_analysis_comment=True),
        ]))
        self.assertEqual(out["requeue_triage"], [])
        self.assertEqual(out["flag_orphaned_analysis"], [])

    def test_ai_triage_without_analysis_not_requeued(self):
        # An issue still in `ai-triage` (awaiting triage) with no analysis yet
        # is the normal pre-triage state, NOT drift.
        out = run(payload([
            issue(569, labels=["ai-triage"], has_analysis_comment=False),
        ]))
        self.assertEqual(out["requeue_triage"], [])

    def test_requeue_triage_is_cron_only(self):
        # Same event-path caution as `requeue` (#319): the auto-fix is withheld
        # on the event-triggered sweep and only emitted by the cron backstop.
        pending = payload([
            issue(569, labels=["pending-approval"], has_analysis_comment=False)])
        self.assertEqual(
            reconcile.process(pending, events_only=True)["requeue_triage"], [])
        self.assertEqual(
            numbers(reconcile.process(pending, events_only=False)["requeue_triage"]),
            [569])

    # --- rule (b): analysis-without-label -> flag_orphaned_analysis --------
    def test_analysis_without_state_label_flags(self):
        out = run(payload([
            issue(570, labels=["type:bug", "area:ai"],
                  has_analysis_comment=True),
        ]))
        self.assertEqual(numbers(out["flag_orphaned_analysis"]), [570])
        # Flag only — never auto-fixed (ambiguous which hand-back state).
        self.assertEqual(out["requeue_triage"], [])

    def test_analysis_with_state_label_not_flagged(self):
        for state in ("pending-approval", "needs-clarification",
                      "ready-for-work", "in-progress", "ai-triage"):
            out = run(payload([
                issue(570, labels=[state], has_analysis_comment=True)]))
            self.assertEqual(out["flag_orphaned_analysis"], [], state)

    def test_no_analysis_no_state_label_not_flagged(self):
        out = run(payload([
            issue(570, labels=["type:bug"], has_analysis_comment=False)]))
        self.assertEqual(out["flag_orphaned_analysis"], [])

    def test_flag_orphaned_analysis_fires_in_events_only_mode(self):
        # Flags are read-only and unaffected by events_only (like flag_done).
        out = reconcile.process(payload([
            issue(570, labels=["type:bug"], has_analysis_comment=True)]),
            events_only=True)
        self.assertEqual(numbers(out["flag_orphaned_analysis"]), [570])

    def test_closed_issue_with_analysis_no_new_findings(self):
        # A closed issue is handled by strip_labels only; the new open-issue
        # rules never fire on it.
        out = run(payload([
            issue(570, state="closed", labels=["pending-approval"],
                  has_analysis_comment=False)]))
        self.assertEqual(out["requeue_triage"], [])
        self.assertEqual(out["flag_orphaned_analysis"], [])
        self.assertEqual(numbers(out["strip_labels"]), [570])

    def test_healthy_triaged_issue_triggers_neither_rule(self):
        # Regression: an issue correctly holding BOTH its analysis comment and
        # its state label triggers neither new rule (mirrors the healthy-board
        # test for the existing rules).
        out = run(payload([
            issue(568, labels=["pending-approval"], has_analysis_comment=True),
            issue(571, labels=["ready-for-work"], has_analysis_comment=True,
                  milestone="v0.12"),
        ]))
        self.assertEqual(out["requeue_triage"], [])
        self.assertEqual(out["flag_orphaned_analysis"], [])


if __name__ == "__main__":
    unittest.main()
