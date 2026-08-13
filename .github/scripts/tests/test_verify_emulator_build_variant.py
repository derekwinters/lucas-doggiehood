"""Unit tests for the emulator-variant release gate (issue #706).

The `v0.14.0` release shipped `doggiehood-v0.14.0-emulator.apk` as a
**byte-for-byte copy** of the device APK: same SHA-256, the bare
`com.derekwinters.doggiehood` applicationId, and `arm64-v8a` native libs. None
of `EmulatorBuildProcessor`'s mutations reached it, because the second
`unity-builder` step in `build-and-attach` was served from the first build's
incremental player cache. Nothing failed — the job went green and uploaded the
wrong file.

`verify_emulator_build_variant.py` is the gate that makes that impossible to
ship again. These tests pin its three pure pieces: `read_manifest_package`
(pulling the applicationId out of an APK's binary AndroidManifest.xml),
`read_apk_facts` (the zip-level read), and `assess` (the pass/fail decision).

`read_manifest_package` is tested against the **real** manifest extracted from
the shipped `doggiehood-v0.14.0-emulator.apk`, committed as a fixture — so the
parser is proven against genuine Unity/aapt output rather than against a
synthetic document this repo also authored (CLAUDE.md rule #6). That fixture
carries the bug itself: its package id is the bare, unsuffixed one.
"""

import hashlib
import os
import re
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from verify_emulator_build_variant import (  # noqa: E402
    ApkFacts,
    MalformedManifest,
    assess,
    read_apk_facts,
    read_manifest_package,
)

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")

# The AndroidManifest.xml extracted verbatim from the shipped
# doggiehood-v0.14.0-emulator.apk release asset (sha256 b45eff45…9691609).
SHIPPED_EMULATOR_MANIFEST = os.path.join(
    FIXTURES, "doggiehood-v0.14.0-emulator-AndroidManifest.axml"
)

DEVICE_ID = "com.derekwinters.doggiehood"
EMULATOR_ID = "com.derekwinters.doggiehood.emulator"


def _fixture_bytes():
    with open(SHIPPED_EMULATOR_MANIFEST, "rb") as handle:
        return handle.read()


def _healthy_pair():
    """The facts a correctly-built release pair would produce."""
    device = ApkFacts(
        path="doggiehood-v9.9.9.apk",
        digest="a" * 64,
        package=DEVICE_ID,
        abis=frozenset({"arm64-v8a"}),
    )
    emulator = ApkFacts(
        path="doggiehood-v9.9.9-emulator.apk",
        digest="b" * 64,
        package=EMULATOR_ID,
        abis=frozenset({"x86_64"}),
    )
    return device, emulator


class ReadManifestPackageTests(unittest.TestCase):
    def test_reads_application_id_from_a_real_shipped_manifest(self):
        # The shipped v0.14.0 emulator asset carries the *bare* id — this is
        # the defect #706 reports, pinned here as the parser's real-world case.
        self.assertEqual(read_manifest_package(_fixture_bytes()), DEVICE_ID)

    def test_rejects_a_document_that_is_not_binary_xml(self):
        with self.assertRaises(MalformedManifest):
            read_manifest_package(b'<?xml version="1.0"?><manifest/>')

    def test_rejects_a_truncated_manifest(self):
        with self.assertRaises(MalformedManifest):
            read_manifest_package(_fixture_bytes()[:64])


class ReadApkFactsTests(unittest.TestCase):
    def _write_apk(self, directory, name, abis):
        path = os.path.join(directory, name)
        with zipfile.ZipFile(path, "w") as apk:
            apk.writestr("AndroidManifest.xml", _fixture_bytes())
            for abi in abis:
                apk.writestr("lib/{0}/libunity.so".format(abi), b"\x00stub")
        return path

    def test_reads_package_abis_and_digest_from_an_apk(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self._write_apk(directory, "sample.apk", ["arm64-v8a"])
            facts = read_apk_facts(path)

            self.assertEqual(facts.package, DEVICE_ID)
            self.assertEqual(facts.abis, frozenset({"arm64-v8a"}))
            with open(path, "rb") as handle:
                self.assertEqual(facts.digest, hashlib.sha256(handle.read()).hexdigest())

    def test_reports_every_abi_directory_present(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self._write_apk(directory, "fat.apk", ["arm64-v8a", "x86_64"])
            self.assertEqual(read_apk_facts(path).abis, frozenset({"arm64-v8a", "x86_64"}))

    def test_rejects_an_apk_with_no_manifest(self):
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "empty.apk")
            with zipfile.ZipFile(path, "w") as apk:
                apk.writestr("classes.dex", b"stub")
            with self.assertRaises(MalformedManifest):
                read_apk_facts(path)


class AssessTests(unittest.TestCase):
    def test_a_correctly_built_pair_passes(self):
        verdict = assess(*_healthy_pair())
        self.assertTrue(verdict.ok, verdict.reasons)
        self.assertEqual(verdict.reasons, [])

    def test_identical_apks_fail(self):
        # The exact v0.14.0 defect: one file uploaded under two names.
        device, emulator = _healthy_pair()
        emulator = emulator._replace(
            digest=device.digest, package=device.package, abis=device.abis
        )
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertTrue(any("identical" in reason for reason in verdict.reasons))

    def test_emulator_apk_without_the_suffix_fails(self):
        device, emulator = _healthy_pair()
        emulator = emulator._replace(package=DEVICE_ID)
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertTrue(any(".emulator" in reason for reason in verdict.reasons))

    def test_emulator_apk_carrying_device_abis_fails(self):
        device, emulator = _healthy_pair()
        emulator = emulator._replace(abis=frozenset({"arm64-v8a"}))
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertTrue(any("arm64-v8a" in reason for reason in verdict.reasons))

    def test_device_apk_that_picked_up_the_suffix_fails(self):
        # The emulator profile must never leak into the device build — if
        # RestoreIfApplied ever stopped working, this is what it would look like.
        device, emulator = _healthy_pair()
        device = device._replace(package=EMULATOR_ID)
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertTrue(any("device" in reason.lower() for reason in verdict.reasons))

    def test_device_apk_that_picked_up_the_emulator_abi_fails(self):
        device, emulator = _healthy_pair()
        device = device._replace(abis=frozenset({"x86_64"}))
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertTrue(any("device" in reason.lower() for reason in verdict.reasons))

    def test_every_independent_failure_is_reported_not_just_the_first(self):
        device, emulator = _healthy_pair()
        emulator = emulator._replace(package=DEVICE_ID, abis=frozenset({"arm64-v8a"}))
        verdict = assess(device, emulator)

        self.assertFalse(verdict.ok)
        self.assertEqual(len(verdict.reasons), 2)


class ReleaseWorkflowsRequestTheEmulatorProfileTests(unittest.TestCase):
    """The gate above proves the *artifact* is right; these prove the *request*
    reaches Unity in the first place (issue #731).

    `v0.15.0` shipped with **no APKs at all**: both builds were device builds,
    so the gate correctly refused to upload either. The cause was that the
    emulator step asked for the variant with a step `env:` entry,
    `DOGGIEHOOD_EMULATOR_BUILD: "true"`. `game-ci/unity-builder` does not run
    Unity in that step's process — it runs it inside a Docker container and
    forwards only a fixed allowlist of variables (`UNITY_*`, `BUILD_*`,
    `ANDROID_*`, `CUSTOM_PARAMETERS`, `GITHUB_*`, `RUNNER_*`), so a
    repo-specific variable is stranded on the runner and the processor no-ops.
    `customParameters` is on that allowlist and is appended verbatim to the
    `unity-editor` command line, which is the channel that actually arrives.

    These tests read the switch name out of `EmulatorBuildProfile.cs`, so a
    rename on either side of the wiring — the C# constant or the workflow —
    fails here rather than silently producing a second device build. They run
    on every PR in `ci-tests.yml` and again in both release jobs before the
    gate is trusted.
    """

    REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.dirname(os.path.abspath(__file__)))))
    PROFILE_SOURCE = os.path.join(
        REPO_ROOT, "Assets", "Scripts", "Core", "Versioning", "EmulatorBuildProfile.cs")
    RELEASE_WORKFLOWS = ("release-please.yml", "release-build.yml")
    EMULATOR_STEP_NAME = "Build Android emulator APK"
    DEVICE_STEP_NAME = "Build Android release APK"

    def _command_line_flag(self):
        """The `-doggiehoodEmulatorBuild` switch, read from the C# constant."""
        with open(self.PROFILE_SOURCE, encoding="utf-8") as handle:
            source = handle.read()
        match = re.search(
            r'CommandLineFlag\s*=\s*"([^"]+)"', source)
        self.assertIsNotNone(
            match, "EmulatorBuildProfile.CommandLineFlag is no longer a string constant")
        return match.group(1)

    def _step_body(self, workflow, step_name):
        """The YAML lines of one named step, up to the next step at its indent."""
        path = os.path.join(self.REPO_ROOT, ".github", "workflows", workflow)
        with open(path, encoding="utf-8") as handle:
            lines = handle.read().splitlines()

        start = None
        indent = None
        for index, line in enumerate(lines):
            stripped = line.strip()
            if stripped == "- name: {0}".format(step_name):
                start = index
                indent = len(line) - len(line.lstrip())
                break
        self.assertIsNotNone(
            start, "{0} has no '{1}' step".format(workflow, step_name))

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

    def test_every_release_workflow_passes_the_switch_on_the_unity_command_line(self):
        flag = self._command_line_flag()
        for workflow in self.RELEASE_WORKFLOWS:
            with self.subTest(workflow=workflow):
                body = self._step_body(workflow, self.EMULATOR_STEP_NAME)
                self.assertIn(
                    flag,
                    self._custom_parameters(body),
                    "{0}'s emulator build step must request the profile with "
                    "customParameters: {1} — a step env: entry never reaches "
                    "Unity inside game-ci's container (#731)".format(workflow, flag),
                )

    def test_no_release_workflow_passes_the_switch_to_the_device_build(self):
        # The mirror of the gate's "device APK picked up the suffix" case: the
        # device APK must never be built with the emulator profile.
        flag = self._command_line_flag()
        for workflow in self.RELEASE_WORKFLOWS:
            with self.subTest(workflow=workflow):
                body = self._step_body(workflow, self.DEVICE_STEP_NAME)
                self.assertNotIn(flag, self._custom_parameters(body))


if __name__ == "__main__":
    unittest.main()
