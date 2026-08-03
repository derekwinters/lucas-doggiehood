#!/usr/bin/env python3
"""EditMode results gate — trust the test outcome, not the teardown exit code.

**Why this exists (issue #534, the #517 CI flake).** The Unity EditMode suite
runs in CI through `game-ci/unity-test-runner`. That action sometimes returns a
nonzero docker exit code while the editor *tears down* — license return and
batch-mode shutdown — *after* a fully green run: the results XML is written and
every test-case reads `result="Passed"`, yet the step is marked failed. On
PR #534 this flaked four runs in a row (2× rerun, 1× empty commit, plus one),
blocking a merge on a passing suite. Re-running does not help, because the flake
is in the teardown, not the tests.

**What this does.** After the runner step (which is allowed to "fail"), this
script re-derives the real verdict from the NUnit3 results XML the runner leaves
in its artifacts directory. If the suite actually ran and every test passed, the
gate is green regardless of the runner's exit code; a teardown-only flake no
longer fails the required check.

**What it must NOT do — the #163 invariant.** A required check must never go
green without the suite *actually running*. So this gate fails, not passes, when
there is no results document, when the results say zero tests ran, or when any
test failed. The teardown exit code is the *only* thing it forgives; a genuine
failure — or a run that died before producing results (the earlier flake mode,
where the editor never started) — still fails the job.

The two decisions — reading one results document (`parse_run`) and the pass/fail
call over the parsed runs (`assess`) — are pure functions so they can be
unit-tested without a Unity checkout; `evaluate_directory` and `main` wire them
to the filesystem.
"""

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from collections import namedtuple

# NUnit3 writes one root <test-run> element carrying the aggregate counts. The
# runner names EditMode output `editmode-results.xml`, but we discover by root
# tag rather than filename so a rename or an extra artifact can't fool the gate.
RUN_ROOT_TAG = "test-run"

RunSummary = namedtuple("RunSummary", ("total", "passed", "failed", "result", "failed_names"))
Verdict = namedtuple("Verdict", ("ok", "total", "failed", "reason"))


class MalformedResults(Exception):
    """Raised when a document is not a well-formed NUnit3 <test-run>."""


def parse_run(xml_text):
    """Parse one NUnit3 results document into a `RunSummary`.

    Raises `MalformedResults` if the text is not well-formed XML or its root is
    not a <test-run> element (e.g. a coverage report) — an unreadable or
    unexpected document is treated as "no usable result", never as a pass.
    """
    try:
        root = ET.fromstring(xml_text)
    except ET.ParseError as exc:
        raise MalformedResults("results XML is not well-formed: {}".format(exc))

    if root.tag != RUN_ROOT_TAG:
        raise MalformedResults(
            "expected a <{}> root, got <{}>".format(RUN_ROOT_TAG, root.tag)
        )

    def _int(attr):
        try:
            return int(root.get(attr, "0"))
        except ValueError:
            return 0

    failed_names = [
        case.get("fullname") or case.get("name") or "<unnamed>"
        for case in root.iter("test-case")
        if case.get("result") == "Failed"
    ]

    return RunSummary(
        total=_int("total"),
        passed=_int("passed"),
        failed=_int("failed"),
        result=root.get("result", ""),
        failed_names=failed_names,
    )


def assess(summaries):
    """Decide the gate over the parsed runs.

    Green only when the suite ran and everything passed:
      * at least one results document exists (the suite produced output),
      * the aggregate test count is > 0 (tests actually ran — #163), and
      * no run has any failure, by count or by explicit `result="Failed"`.
    Anything else is red, with a human-readable reason.
    """
    if not summaries:
        return Verdict(False, 0, 0, "no EditMode results document was produced "
                       "(the suite did not run to completion)")

    total = sum(s.total for s in summaries)
    failed = sum(s.failed for s in summaries)
    failed_names = [name for s in summaries for name in s.failed_names]
    any_failed_verdict = any(s.result == "Failed" for s in summaries)

    if total <= 0:
        return Verdict(False, total, failed, "the results document reports zero "
                       "tests run (the suite did not execute)")

    if failed > 0 or any_failed_verdict:
        detail = ": " + ", ".join(failed_names) if failed_names else ""
        return Verdict(False, total, failed,
                       "{} EditMode test(s) failed{}".format(failed, detail))

    return Verdict(True, total, failed,
                   "all {} EditMode tests passed".format(total))


def _iter_run_documents(directory):
    """Yield parsed `RunSummary` for each NUnit3 <test-run> file under
    `directory`. Non-test-run XML (coverage) and unreadable files are skipped;
    the emptiness of the result is what `assess` turns into a failure."""
    if not os.path.isdir(directory):
        return
    for dirpath, _dirnames, filenames in os.walk(directory):
        for name in sorted(filenames):
            if not name.endswith(".xml"):
                continue
            path = os.path.join(dirpath, name)
            try:
                with open(path, "r", encoding="utf-8") as handle:
                    text = handle.read()
                yield parse_run(text)
            except (OSError, MalformedResults):
                continue


def evaluate_directory(directory):
    """Filesystem entry point: assess every <test-run> document under
    `directory` and return the combined `Verdict`."""
    return assess(list(_iter_run_documents(directory)))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "results_dir",
        nargs="?",
        default="artifacts",
        help="Directory the test runner wrote its NUnit results into "
             "(default: artifacts).",
    )
    args = parser.parse_args(argv)

    verdict = evaluate_directory(args.results_dir)

    if verdict.ok:
        print("EditMode results gate PASSED — {} (ignoring any teardown-only "
              "exit code).".format(verdict.reason))
        return 0

    print("EditMode results gate FAILED — {}.".format(verdict.reason))
    print("Looked for NUnit <test-run> results under: {}".format(
        os.path.abspath(args.results_dir)))
    return 1


if __name__ == "__main__":
    sys.exit(main())
