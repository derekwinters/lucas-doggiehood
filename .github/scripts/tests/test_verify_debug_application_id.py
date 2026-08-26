"""Unit tests for the debug-suffix build gate (issue #734).

`pr-build.yml` and `rc-build.yml` asked for the `.debug` applicationId suffix
with a step `env:` entry, `DOGGIEHOOD_DEBUG_BUILD: "true"`. `game-ci/unity-builder`
does not run Unity in that step's process — it runs it inside a Docker container
and forwards only a fixed allowlist of variables (`UNITY_*`, `BUILD_*`,
`ANDROID_*`, `CUSTOM_PARAMETERS`, `GITHUB_*`, `RUNNER_*`) — so the variable was
stranded on the runner, `DebugApplicationIdBuildProcessor` no-op'd, and every PR
and RC APK shipped the bare `com.derekwinters.doggiehood` id. Installed next to
a release build it would have *replaced* it rather than sitting beside it, which
is the one thing the suffix exists to prevent.

Nothing ever checked, which is why it survived. These tests pin the two halves
that now do:

* `verify_debug_application_id.py` — the pre-upload gate. Its decision
  (`assess`) is a pure function over the same `ApkFacts` the emulator gate
  reads, so it is tested here without a Unity checkout or a real build. It
  deliberately *reuses* `verify_emulator_build_variant`'s binary-manifest
  parser rather than carrying a second copy, and one test pins that.
* The workflow wiring — that both debug-building workflows request the suffix
  on Unity's command line and run the gate before uploading. A rename on either
  side of that wiring fails here rather than silently shipping a mislabelled APK.
"""

import os
import re
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import verify_emulator_build_variant  # noqa: E402

import verify_debug_application_id  # noqa: E402
from verify_debug_application_id import (  # noqa: E402
    DEBUG_APPLICATION_ID_SUFFIX,
    ApkFacts,
    MalformedManifest,
    assess,
    read_apk_facts,
)

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")

# The AndroidManifest.xml extracted verbatim from the shipped
# doggiehood-v0.14.0-emulator.apk release asset. It carries the *bare*
# applicationId, which is exactly what an unsuffixed debug build looks like —
# so it doubles as this gate's real-world failing case.
BARE_ID_MANIFEST = os.path.join(
    FIXTURES, "doggiehood-v0.14.0-emulator-AndroidManifest.axml"
)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))))
SUFFIX_SOURCE = os.path.join(
    REPO_ROOT, "Assets", "Scripts", "Core", "Versioning", "ApplicationIdSuffix.cs")

BARE_ID = "com.derekwinters.doggiehood"
DEBUG_ID = "com.derekwinters.doggiehood.debug"


def _fixture_bytes():
    with open(BARE_ID_MANIFEST, "rb") as handle:
        return handle.read()


def _facts(package):
    return ApkFacts(
        path="Doggiehood.apk",
        digest="a" * 64,
        package=package,
        abis=frozenset({"arm64-v8a"}),
    )


class ParserReuseTests(unittest.TestCase):
    def test_the_gate_reuses_the_emulator_gates_manifest_reader(self):
        # There is exactly one binary-AndroidManifest parser in this repo. A
        # second copy would drift from the one that is tested against a real
        # shipped manifest, so the reuse is pinned rather than assumed.
        self.assertIs(read_apk_facts, verify_emulator_build_variant.read_apk_facts)
        self.assertIs(
            verify_debug_application_id.read_manifest_package,
            verify_emulator_build_variant.read_manifest_package,
        )

    def test_the_expected_suffix_matches_the_one_the_editor_applies(self):
        # ApplicationIdSuffix.Debug is what the build hook appends; the gate
        # must look for that same string, not a hand-copied guess.
        with open(SUFFIX_SOURCE, encoding="utf-8") as handle:
            source = handle.read()
        match = re.search(r'Debug\s*=\s*"([^"]+)"', source)
        self.assertIsNotNone(match, "ApplicationIdSuffix.Debug is no longer a string constant")
        self.assertEqual(DEBUG_APPLICATION_ID_SUFFIX, match.group(1))


class AssessTests(unittest.TestCase):
    def test_a_suffixed_apk_passes(self):
        verdict = assess(_facts(DEBUG_ID))

        self.assertTrue(verdict.ok, verdict.reasons)
        self.assertEqual(verdict.reasons, [])

    def test_a_bare_application_id_fails(self):
        # The exact #734 defect: the flag never reached Unity, so the build
        # produced a release-id APK under a debug artifact name.
        verdict = assess(_facts(BARE_ID))

        self.assertFalse(verdict.ok)
        self.assertTrue(any(DEBUG_APPLICATION_ID_SUFFIX in reason for reason in verdict.reasons))
        self.assertTrue(any(BARE_ID in reason for reason in verdict.reasons))

    def test_a_double_suffixed_application_id_fails(self):
        # A restore that stopped working would compound the suffix; an APK
        # nobody can upgrade in place is not a passing build either.
        verdict = assess(_facts(BARE_ID + ".debug.debug"))

        self.assertFalse(verdict.ok)

    def test_a_different_application_id_fails(self):
        verdict = assess(_facts("com.example.other.debug"))

        self.assertFalse(verdict.ok)
        self.assertTrue(any(BARE_ID in reason for reason in verdict.reasons))


class ReadApkFactsTests(unittest.TestCase):
    def _write_apk(self, directory, name):
        path = os.path.join(directory, name)
        with zipfile.ZipFile(path, "w") as apk:
            apk.writestr("AndroidManifest.xml", _fixture_bytes())
            apk.writestr("lib/arm64-v8a/libunity.so", b"\x00stub")
        return path

    def test_a_real_unsuffixed_apk_is_rejected_end_to_end(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self._write_apk(directory, "Doggiehood.apk")

            facts = read_apk_facts(path)
            self.assertEqual(facts.package, BARE_ID)
            self.assertFalse(assess(facts).ok)

    def test_an_unreadable_apk_is_never_treated_as_a_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "empty.apk")
            with zipfile.ZipFile(path, "w") as apk:
                apk.writestr("classes.dex", b"stub")

            with self.assertRaises(MalformedManifest):
                read_apk_facts(path)


class DebugWorkflowsRequestTheSuffixTests(unittest.TestCase):
    """The gate above proves the *artifact* is right; these prove the *request*
    reaches Unity in the first place, and that the gate actually runs.

    The switch name is read out of `ApplicationIdSuffix.CommandLineFlag`, so a
    rename on either side of the wiring — the C# constant or the workflow —
    fails here rather than silently producing an unsuffixed APK.
    """

    DEBUG_WORKFLOWS = {
        "pr-build.yml": "Build Android debug APK",
        "rc-build.yml": "Build Android RC APK",
    }
    UPLOAD_STEP_NAME = "Upload APK artifact"
    GATE_SCRIPT = "verify_debug_application_id.py"
    RELEASE_WORKFLOWS = ("release-please.yml", "release-build.yml")

    def _command_line_flag(self):
        """The `-doggiehoodDebugBuild` switch, read from the C# constant."""
        with open(SUFFIX_SOURCE, encoding="utf-8") as handle:
            source = handle.read()
        match = re.search(r'CommandLineFlag\s*=\s*"([^"]+)"', source)
        self.assertIsNotNone(
            match, "ApplicationIdSuffix.CommandLineFlag is no longer a string constant")
        return match.group(1)

    def _lines(self, workflow):
        path = os.path.join(REPO_ROOT, ".github", "workflows", workflow)
        with open(path, encoding="utf-8") as handle:
            return handle.read().splitlines()

    def _step_index(self, lines, workflow, step_name):
        for index, line in enumerate(lines):
            if line.strip() == "- name: {0}".format(step_name):
                return index
        self.fail("{0} has no '{1}' step".format(workflow, step_name))

    def _step_body(self, lines, workflow, step_name):
        """The YAML lines of one named step, up to the next step at its indent."""
        start = self._step_index(lines, workflow, step_name)
        indent = len(lines[start]) - len(lines[start].lstrip())

        body = [lines[start]]
        for line in lines[start + 1:]:
            bare = line.strip()
            if bare.startswith("- ") and (len(line) - len(line.lstrip())) == indent:
                break
            body.append(line)
        return body

    def _custom_parameters(self, body):
        """The `customParameters:` tokens declared in a step body, if any."""
        for line in body:
            stripped = line.strip()
            if stripped.startswith("#"):
                continue
            if stripped.startswith("customParameters:"):
                return stripped.split(":", 1)[1].split()
        return []

    def test_every_debug_workflow_passes_the_switch_on_the_unity_command_line(self):
        flag = self._command_line_flag()
        for workflow, step in self.DEBUG_WORKFLOWS.items():
            with self.subTest(workflow=workflow):
                body = self._step_body(self._lines(workflow), workflow, step)
                self.assertIn(
                    flag,
                    self._custom_parameters(body),
                    "{0}'s build step must request the suffix with "
                    "customParameters: {1} — a step env: entry never reaches "
                    "Unity inside game-ci's container (#734)".format(workflow, flag),
                )

    def test_every_debug_workflow_runs_the_gate_before_uploading(self):
        for workflow in self.DEBUG_WORKFLOWS:
            with self.subTest(workflow=workflow):
                lines = self._lines(workflow)
                gate = [
                    index for index, line in enumerate(lines)
                    if self.GATE_SCRIPT in line and not line.strip().startswith("#")
                ]
                self.assertTrue(
                    gate,
                    "{0} never runs {1}, so a silent no-op would upload a "
                    "mislabelled APK again (#734)".format(workflow, self.GATE_SCRIPT),
                )
                upload = self._step_index(lines, workflow, self.UPLOAD_STEP_NAME)
                self.assertLess(
                    min(gate), upload,
                    "{0} must verify the applicationId before uploading the "
                    "artifact, not after".format(workflow),
                )

    def test_no_release_workflow_requests_the_debug_suffix(self):
        # The mirror of the gate's failing case: a release asset carrying
        # `.debug` could never be upgraded in place by a later release. The
        # switch belongs to the PR/RC workflows only.
        flag = self._command_line_flag()
        for workflow in self.RELEASE_WORKFLOWS:
            with self.subTest(workflow=workflow):
                for line in self._lines(workflow):
                    if line.strip().startswith("#"):
                        continue
                    self.assertNotIn(
                        flag, line,
                        "{0} must never request the debug suffix — a release "
                        "APK ships the permanent id".format(workflow),
                    )


if __name__ == "__main__":
    unittest.main()
