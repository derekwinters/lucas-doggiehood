"""Unit tests for the release-signature gate (issue #630).

Every Doggiehood build up to now used Android's *default debug signing*,
deferred deliberately in #75. A debug key is not a stable owned key, so a new
APK can fail to install over an old one with a signature mismatch — and on
Android that forces an uninstall, which wipes `doggiehood-save.txt`. The game
is offline and local-save-only, so that save is the only copy.

`verify_release_signature.py` is the gate that makes shipping such an APK
impossible. It is the automatable half of the guarantee: the release
certificate is pinned in `.github/release-cert-sha256.txt`, and any release
artifact signed by a different key — including a silent fall back to the debug
key, which is exactly what a mis-wired keystore input produces — fails the
release job before either asset is uploaded.

These tests pin the pure pieces: `normalize_fingerprint` (keytool's colon form
and apksigner's bare hex are the same 32 bytes written two ways),
`parse_apksigner_certs` (apksigner output -> signers), and `assess` (the
pass/fail call). They also pin the *wiring*, because the gate is worthless if
the keystore never reaches the builder: both release workflows must hand the
keystore to every release build step and run this gate, and — per Derek's call
on #630 — neither PR-triggered workflow may reference the keystore at all.

A note on the apksigner samples below: unlike the manifest fixture in
`test_verify_emulator_build_variant.py`, these are authored from apksigner's
documented output shape rather than captured from a real run, because no
Android SDK is available in the agent environment. That is why the parser
cross-checks the `Number of signers:` header against the blocks it actually
parsed and raises rather than returning a short list — a format drift fails
loudly instead of quietly reporting "no signers".
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from verify_release_signature import (  # noqa: E402
    MalformedApksignerOutput,
    MalformedFingerprint,
    SignerFacts,
    assess,
    normalize_fingerprint,
    parse_apksigner_certs,
    read_expected_fingerprint,
)

RELEASE_SHA256 = "b7c799ea85dd9a31426b9a432e6a468b43c318857f68ce79fb91bdb1125e3a16"
OTHER_SHA256 = "0123456789abcdef" * 4

RELEASE_OUTPUT = """Verifies
Verified using v1 scheme (JAR signing): true
Verified using v2 scheme (APK Signature Scheme v2): true
Verified using v3 scheme (APK Signature Scheme v3): true
Number of signers: 1
Signer #1 certificate DN: CN=Doggiehood, O=Derek Winters, C=US
Signer #1 certificate SHA-256 digest: {0}
Signer #1 certificate SHA-1 digest: 9f7c1a0b4e2d6f8a1c3b5d7e9f0a2b4c6d8e0f12
Signer #1 certificate MD5 digest: 2b4c6d8e0f1a3b5c7d9e0f1a2b3c4d5e
""".format(RELEASE_SHA256)

DEBUG_OUTPUT = """Verifies
Verified using v1 scheme (JAR signing): true
Number of signers: 1
Signer #1 certificate DN: CN=Android Debug, O=Android, C=US
Signer #1 certificate SHA-256 digest: {0}
""".format(OTHER_SHA256)

TWO_SIGNER_OUTPUT = """Verifies
Number of signers: 2
Signer #1 certificate DN: CN=Doggiehood, O=Derek Winters, C=US
Signer #1 certificate SHA-256 digest: {0}
Signer #2 certificate DN: CN=Somebody Else, O=Elsewhere, C=US
Signer #2 certificate SHA-256 digest: {1}
""".format(RELEASE_SHA256, OTHER_SHA256)


class NormalizeFingerprintTests(unittest.TestCase):
    """keytool and apksigner print the same 32 bytes in different shapes.

    `keytool -list -v` gives `SHA256: B7:C7:...` — colon-separated, uppercase,
    behind a label. apksigner prints bare lowercase hex. Both are SHA-256 over
    the DER-encoded certificate, so they are directly comparable once written
    the same way; this function is what makes pasting either form into
    `.github/release-cert-sha256.txt` safe.
    """

    def test_accepts_the_keytool_colon_form(self):
        keytool_form = "B7:C7:99:EA:85:DD:9A:31:42:6B:9A:43:2E:6A:46:8B:43:C3:18:85:7F:68:CE:79:FB:91:BD:B1:12:5E:3A:16"
        self.assertEqual(normalize_fingerprint(keytool_form), RELEASE_SHA256)

    def test_accepts_apksigners_bare_hex(self):
        self.assertEqual(normalize_fingerprint(RELEASE_SHA256), RELEASE_SHA256)

    def test_accepts_a_leading_sha256_label_and_surrounding_whitespace(self):
        labelled = "  SHA256: B7:C7:99:EA:85:DD:9A:31:42:6B:9A:43:2E:6A:46:8B:43:C3:18:85:7F:68:CE:79:FB:91:BD:B1:12:5E:3A:16\n"
        self.assertEqual(normalize_fingerprint(labelled), RELEASE_SHA256)

    def test_rejects_a_fingerprint_of_the_wrong_length(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("B7:C7:99:EA")

    def test_rejects_a_fingerprint_that_is_not_hex(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("zz" + RELEASE_SHA256[2:])

    def test_rejects_an_empty_fingerprint(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("   \n")


class ParseApksignerCertsTests(unittest.TestCase):
    def test_reads_the_dn_and_digest_of_a_single_signer(self):
        signers = parse_apksigner_certs(RELEASE_OUTPUT)
        self.assertEqual(len(signers), 1)
        self.assertEqual(signers[0].index, 1)
        self.assertEqual(signers[0].dn, "CN=Doggiehood, O=Derek Winters, C=US")
        self.assertEqual(signers[0].sha256, RELEASE_SHA256)

    def test_reads_every_signer(self):
        signers = parse_apksigner_certs(TWO_SIGNER_OUTPUT)
        self.assertEqual([signer.index for signer in signers], [1, 2])
        self.assertEqual(
            [signer.sha256 for signer in signers], [RELEASE_SHA256, OTHER_SHA256])

    def test_uppercase_digests_are_normalized(self):
        signers = parse_apksigner_certs(
            RELEASE_OUTPUT.replace(RELEASE_SHA256, RELEASE_SHA256.upper()))
        self.assertEqual(signers[0].sha256, RELEASE_SHA256)

    def test_reports_no_signers_when_apksigner_found_none(self):
        self.assertEqual(parse_apksigner_certs("Number of signers: 0\n"), [])

    def test_a_signer_count_that_disagrees_with_the_blocks_raises(self):
        """Format drift must fail loudly, not silently report fewer signers."""
        truncated = TWO_SIGNER_OUTPUT.replace(
            "Signer #2 certificate DN: CN=Somebody Else, O=Elsewhere, C=US\n", "")
        truncated = truncated.replace(
            "Signer #2 certificate SHA-256 digest: {0}\n".format(OTHER_SHA256), "")
        with self.assertRaises(MalformedApksignerOutput):
            parse_apksigner_certs(truncated)

    def test_output_without_a_signer_count_raises(self):
        with self.assertRaises(MalformedApksignerOutput):
            parse_apksigner_certs("Verifies\n")


class AssessTests(unittest.TestCase):
    def test_the_expected_certificate_passes(self):
        verdict = assess(parse_apksigner_certs(RELEASE_OUTPUT), RELEASE_SHA256)
        self.assertTrue(verdict.ok, verdict.reasons)
        self.assertEqual(verdict.reasons, [])

    def test_a_different_certificate_fails_and_names_both_fingerprints(self):
        signers = [SignerFacts(index=1, dn="CN=Somebody Else", sha256=OTHER_SHA256)]
        verdict = assess(signers, RELEASE_SHA256)
        self.assertFalse(verdict.ok)
        joined = " ".join(verdict.reasons)
        self.assertIn(OTHER_SHA256, joined)
        self.assertIn(RELEASE_SHA256, joined)

    def test_the_android_debug_certificate_fails_as_a_debug_fallback(self):
        verdict = assess(parse_apksigner_certs(DEBUG_OUTPUT), RELEASE_SHA256)
        self.assertFalse(verdict.ok)
        self.assertTrue(
            any("debug" in reason.lower() for reason in verdict.reasons),
            "a debug-signed release APK must be reported as a debug fallback, "
            "not merely as a fingerprint mismatch: {0}".format(verdict.reasons))

    def test_an_unsigned_apk_fails(self):
        verdict = assess([], RELEASE_SHA256)
        self.assertFalse(verdict.ok)
        self.assertTrue(any("unsigned" in reason.lower() for reason in verdict.reasons))

    def test_an_extra_signer_fails_even_when_the_release_key_is_present(self):
        verdict = assess(parse_apksigner_certs(TWO_SIGNER_OUTPUT), RELEASE_SHA256)
        self.assertFalse(verdict.ok)

    def test_the_expected_fingerprint_may_be_given_in_keytool_form(self):
        verdict = assess(
            parse_apksigner_certs(RELEASE_OUTPUT),
            "B7:C7:99:EA:85:DD:9A:31:42:6B:9A:43:2E:6A:46:8B:43:C3:18:85:7F:68:CE:79:FB:91:BD:B1:12:5E:3A:16")
        self.assertTrue(verdict.ok, verdict.reasons)


class CommittedFingerprintTests(unittest.TestCase):
    """The pinned certificate is repo content, so a bad paste fails here.

    The fingerprint is public — it is in every published APK — which is why it
    is committed rather than held as a secret. What it must never be is
    *wrong*: a typo would fail every release with a mismatch that looks like a
    compromised key.
    """

    REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.dirname(os.path.abspath(__file__)))))
    EXPECTED_FILE = os.path.join(REPO_ROOT, ".github", "release-cert-sha256.txt")

    def test_the_committed_fingerprint_is_a_wellformed_sha256(self):
        fingerprint = read_expected_fingerprint(self.EXPECTED_FILE)
        self.assertEqual(len(fingerprint), 64)
        self.assertEqual(fingerprint, fingerprint.lower())
        int(fingerprint, 16)


class ReleaseWorkflowSigningTests(unittest.TestCase):
    """The gate only protects a release if the keystore reaches the builder.

    Both paths that produce a release asset — `release-please.yml`'s
    `build-and-attach` job and the `release-build.yml` backfill — must sign
    *both* the device and emulator APKs with the release keystore and verify
    the result before uploading. And per the #630 decision, the release
    keystore must never be referenced by a `pull_request`-triggered workflow:
    PR and RC builds stay debug-signed with the `.debug` applicationId suffix
    (#80), so they install side-by-side and never upgrade into the real app.
    """

    REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.dirname(os.path.abspath(__file__)))))
    RELEASE_WORKFLOWS = ("release-please.yml", "release-build.yml")
    PR_TRIGGERED_WORKFLOWS = ("pr-build.yml", "rc-build.yml")
    BUILD_STEPS = ("Build Android release APK", "Build Android emulator APK")
    KEYSTORE_SECRET = "ANDROID_KEYSTORE_BASE64"
    VERIFY_STEP = "Verify the release signature"

    def _workflow_text(self, workflow):
        path = os.path.join(self.REPO_ROOT, ".github", "workflows", workflow)
        with open(path, encoding="utf-8") as handle:
            return handle.read()

    def _step_body(self, workflow, step_name):
        """The YAML lines of one named step, up to the next step at its indent."""
        lines = self._workflow_text(workflow).splitlines()
        start = None
        indent = None
        for index, line in enumerate(lines):
            if line.strip() == "- name: {0}".format(step_name):
                start = index
                indent = len(line) - len(line.lstrip())
                break
        self.assertIsNotNone(
            start, "{0} has no '{1}' step".format(workflow, step_name))

        body = [lines[start]]
        for line in lines[start + 1:]:
            if line.strip().startswith("- ") and (len(line) - len(line.lstrip())) == indent:
                break
            body.append(line)
        return "\n".join(body)

    def test_every_release_build_step_receives_the_release_keystore(self):
        for workflow in self.RELEASE_WORKFLOWS:
            for step in self.BUILD_STEPS:
                with self.subTest(workflow=workflow, step=step):
                    body = self._step_body(workflow, step)
                    self.assertIn(
                        "androidKeystoreBase64", body,
                        "{0}'s '{1}' step does not sign with the release keystore, so "
                        "it would fall back to the debug key (#630)".format(workflow, step))
                    self.assertIn("androidKeyaliasName", body)

    def test_every_release_workflow_verifies_the_signature_before_uploading(self):
        for workflow in self.RELEASE_WORKFLOWS:
            with self.subTest(workflow=workflow):
                text = self._workflow_text(workflow)
                self.assertIn(
                    self.VERIFY_STEP, text,
                    "{0} uploads release assets without checking the signing "
                    "certificate (#630)".format(workflow))
                self.assertLess(
                    text.index(self.VERIFY_STEP), text.index("gh release upload"),
                    "{0} verifies the signature after uploading, which is too "
                    "late".format(workflow))

    def test_no_pull_request_triggered_workflow_touches_the_release_keystore(self):
        for workflow in self.PR_TRIGGERED_WORKFLOWS:
            with self.subTest(workflow=workflow):
                self.assertNotIn(
                    self.KEYSTORE_SECRET, self._workflow_text(workflow),
                    "{0} is pull_request-triggered and must never reach the release "
                    "keystore (#630)".format(workflow))


if __name__ == "__main__":
    unittest.main()
