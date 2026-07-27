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
    rule #10. A bare `#N`, `Refs #N`, `Part of #N`, `Relates to #N` only links.
    """

    def test_prose_and_bare_refs_are_not_closing(self):
        # None of these landed work per rule #10 -> empty closing-ref set.
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
        for key in ("strip_labels", "requeue", "flag_done",
                    "flag_orphaned_ready", "flag_prose_dep"):
            self.assertEqual(out[key], [], key)

    def test_findings_sorted_by_number(self):
        out = run(payload([
            issue(300, labels=["in-progress"]),
            issue(100, labels=["in-progress"]),
            issue(200, labels=["in-progress"]),
        ]))
        self.assertEqual(numbers(out["requeue"]), [100, 200, 300])


if __name__ == "__main__":
    unittest.main()
