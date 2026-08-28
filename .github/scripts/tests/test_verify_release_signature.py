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

A note on the apksigner samples below. The single-signer transcripts are
**captured**, not authored: `fixtures/apksigner-verify-print-certs-verbose.txt`
and `fixtures/apksigner-verify-print-certs-nonverbose.txt` are the verbatim
stdout of apksigner 31.0.2 run over a real signed APK, in the two modes this
gate could have used. That provenance is the whole point of #752. The earlier
fixtures were authored from apksigner's *documented* shape, and every one of
them was a verbose transcript while the workflow invoked the tool without
`--verbose` — so twenty-two green tests certified a gate that could not read a
single real APK, and the v0.16.0 release shipped with no assets at all. A gate
that parses another tool's output is only as trustworthy as the realness of
the output it was tested against.

The captured pair is what pins that now: the verbose transcript is the format
the fixed invocation asks for and the parser reads, and the non-verbose one is
the real output that broke the release — kept as a fixture precisely so the
parser is proven to still reject it. The parser's `Number of signers:`
cross-check is deliberately untouched by the fix: the invocation was wrong,
not the reading, and a genuine future format drift must still fail loudly
rather than quietly report "no signers".

`TWO_SIGNER_OUTPUT` remains hand-authored — a two-key APK is not something
this repo can produce on demand — and is used only for the multi-signer
parse/assess cases.
"""

import os
import subprocess
import sys
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from verify_release_signature import (  # noqa: E402
    MalformedApksignerOutput,
    MalformedFingerprint,
    SignerFacts,
    assess,
    normalize_fingerprint,
    parse_apksigner_certs,
    print_certs,
    read_expected_fingerprint,
)

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")


def _captured(name):
    with open(os.path.join(FIXTURES, name), encoding="utf-8") as handle:
        return handle.read()


# apksigner 31.0.2's verbatim stdout over a real signed APK, in both modes:
#   apksigner verify --print-certs [--verbose] signed.apk
# The verbose one is what the gate now asks for and what the parser reads; the
# non-verbose one is what it used to ask for, and carries no
# "Number of signers:" line at all — the v0.16.0 release failure, captured.
CAPTURED_VERBOSE_OUTPUT = _captured("apksigner-verify-print-certs-verbose.txt")
CAPTURED_NON_VERBOSE_OUTPUT = _captured("apksigner-verify-print-certs-nonverbose.txt")

# The certificate in that captured transcript. It is a throwaway key generated
# solely to produce the capture — deliberately NOT the pinned release
# certificate in `.github/release-cert-sha256.txt`, which no keystore in this
# environment holds. What these fixtures pin is apksigner's output *shape*;
# the pinned fingerprint's own correctness is CommittedFingerprintTests' job.
CAPTURED_SHA256 = "10d315258da7c4c4830814ae6f876e84145f2195cfb077bc0bb04e3df0a61ed8"
CAPTURED_KEYTOOL_FORM = (
    "10:D3:15:25:8D:A7:C4:C4:83:08:14:AE:6F:87:6E:84:"
    "14:5F:21:95:CF:B0:77:BC:0B:B0:4E:3D:F0:A6:1E:D8")
CAPTURED_DN = "CN=Doggiehood Release, O=Derek Winters, L=Somewhere, C=US"

OTHER_SHA256 = "0123456789abcdef" * 4

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
""".format(CAPTURED_SHA256, OTHER_SHA256)


class NormalizeFingerprintTests(unittest.TestCase):
    """keytool and apksigner print the same 32 bytes in different shapes.

    `keytool -list -v` gives `SHA256: 10:D3:...` — colon-separated, uppercase,
    behind a label. apksigner prints bare lowercase hex. Both are SHA-256 over
    the DER-encoded certificate, so they are directly comparable once written
    the same way; this function is what makes pasting either form into
    `.github/release-cert-sha256.txt` safe.
    """

    def test_accepts_the_keytool_colon_form(self):
        self.assertEqual(
            normalize_fingerprint(CAPTURED_KEYTOOL_FORM), CAPTURED_SHA256)

    def test_accepts_apksigners_bare_hex(self):
        self.assertEqual(normalize_fingerprint(CAPTURED_SHA256), CAPTURED_SHA256)

    def test_accepts_a_leading_sha256_label_and_surrounding_whitespace(self):
        labelled = "  SHA256: {0}\n".format(CAPTURED_KEYTOOL_FORM)
        self.assertEqual(normalize_fingerprint(labelled), CAPTURED_SHA256)

    def test_rejects_a_fingerprint_of_the_wrong_length(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("B7:C7:99:EA")

    def test_rejects_a_fingerprint_that_is_not_hex(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("zz" + CAPTURED_SHA256[2:])

    def test_rejects_an_empty_fingerprint(self):
        with self.assertRaises(MalformedFingerprint):
            normalize_fingerprint("   \n")


class ParseApksignerCertsTests(unittest.TestCase):
    def test_reads_the_dn_and_digest_of_a_single_signer_from_a_real_capture(self):
        signers = parse_apksigner_certs(CAPTURED_VERBOSE_OUTPUT)
        self.assertEqual(len(signers), 1)
        self.assertEqual(signers[0].index, 1)
        self.assertEqual(signers[0].dn, CAPTURED_DN)
        self.assertEqual(signers[0].sha256, CAPTURED_SHA256)

    def test_reads_every_signer(self):
        signers = parse_apksigner_certs(TWO_SIGNER_OUTPUT)
        self.assertEqual([signer.index for signer in signers], [1, 2])
        self.assertEqual(
            [signer.sha256 for signer in signers], [CAPTURED_SHA256, OTHER_SHA256])

    def test_uppercase_digests_are_normalized(self):
        signers = parse_apksigner_certs(
            CAPTURED_VERBOSE_OUTPUT.replace(CAPTURED_SHA256, CAPTURED_SHA256.upper()))
        self.assertEqual(signers[0].sha256, CAPTURED_SHA256)

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

    def test_real_non_verbose_output_is_rejected_the_way_it_broke_v0_16_0(self):
        """The parser's format check stays strict — the fix was the flags.

        This is apksigner's genuine `--print-certs` output with no `--verbose`:
        the signer DN and digest blocks are all there, and the
        `Number of signers:` header is simply absent. Feeding it to the parser
        reproduces the exact error that failed the v0.16.0 release job.

        It must keep raising. #752 is fixed by asking apksigner for the format
        the parser reads, *not* by teaching the parser to accept a header-less
        one — a loosened parser would swallow a real future format drift, which
        is the failure this cross-check exists to make loud.
        """
        self.assertNotIn("Number of signers:", CAPTURED_NON_VERBOSE_OUTPUT)
        self.assertIn("certificate SHA-256 digest:", CAPTURED_NON_VERBOSE_OUTPUT)
        with self.assertRaises(MalformedApksignerOutput) as raised:
            parse_apksigner_certs(CAPTURED_NON_VERBOSE_OUTPUT)
        self.assertIn("Number of signers:", str(raised.exception))

    def test_the_captured_verbose_output_carries_the_header_the_parser_needs(self):
        """The two halves of the fix, pinned against each other.

        The fixture is the output of the very command line `print_certs` now
        runs, so this asserts the invocation and the parser agree on one
        format — the pairing nothing checked before #752.
        """
        self.assertIn("Number of signers: 1", CAPTURED_VERBOSE_OUTPUT)
        signers = parse_apksigner_certs(CAPTURED_VERBOSE_OUTPUT)
        self.assertEqual([signer.sha256 for signer in signers], [CAPTURED_SHA256])

    def test_the_verbose_only_extra_lines_do_not_confuse_the_parser(self):
        """`--verbose` adds public-key lines; none may read as a certificate."""
        self.assertIn("Signer #1 public key SHA-256 digest:", CAPTURED_VERBOSE_OUTPUT)
        signers = parse_apksigner_certs(CAPTURED_VERBOSE_OUTPUT)
        self.assertEqual(len(signers), 1)
        self.assertEqual(signers[0].sha256, CAPTURED_SHA256)


class ApksignerInvocationTests(unittest.TestCase):
    """The command line the gate runs must produce the format it parses.

    This is the bug of #752, pinned. `parse_apksigner_certs` requires the
    `Number of signers:` header, and apksigner prints that header **only**
    under `--verbose`: `--print-certs` alone emits the signer DN and SHA-256
    blocks and nothing else. So the gate asked for one format and read
    another, and failed every real APK it was ever pointed at — the v0.16.0
    release included, which shipped with no assets at all.

    Twenty-two green tests did not catch it because they all exercised the
    parser against hand-authored *verbose* transcripts while nothing pinned
    the invocation. This test is that pin: it asserts the flags, not the
    parse.
    """

    def _run_print_certs(self):
        completed = subprocess.CompletedProcess(
            args=[], returncode=0, stdout=CAPTURED_VERBOSE_OUTPUT, stderr="")
        with mock.patch("verify_release_signature.subprocess.run",
                        return_value=completed) as run:
            print_certs("doggiehood-v9.9.9.apk", apksigner="/usr/bin/apksigner")
        self.assertEqual(run.call_count, 1)
        return list(run.call_args[0][0])

    def test_the_invocation_asks_for_the_verbose_output_the_parser_reads(self):
        argv = self._run_print_certs()
        self.assertIn(
            "--verbose", argv,
            "apksigner prints 'Number of signers:' only under --verbose, and "
            "parse_apksigner_certs requires that line — without the flag the "
            "gate cannot read any real APK (#752): {0}".format(argv))

    def test_the_invocation_still_verifies_and_prints_certs_for_the_apk(self):
        argv = self._run_print_certs()
        self.assertEqual(argv[0], "/usr/bin/apksigner")
        self.assertIn("verify", argv)
        self.assertIn("--print-certs", argv)
        self.assertEqual(argv[-1], "doggiehood-v9.9.9.apk")


class AssessTests(unittest.TestCase):
    def test_the_expected_certificate_passes(self):
        verdict = assess(parse_apksigner_certs(CAPTURED_VERBOSE_OUTPUT), CAPTURED_SHA256)
        self.assertTrue(verdict.ok, verdict.reasons)
        self.assertEqual(verdict.reasons, [])

    def test_a_different_certificate_fails_and_names_both_fingerprints(self):
        signers = [SignerFacts(index=1, dn="CN=Somebody Else", sha256=OTHER_SHA256)]
        verdict = assess(signers, CAPTURED_SHA256)
        self.assertFalse(verdict.ok)
        joined = " ".join(verdict.reasons)
        self.assertIn(OTHER_SHA256, joined)
        self.assertIn(CAPTURED_SHA256, joined)

    def test_the_android_debug_certificate_fails_as_a_debug_fallback(self):
        verdict = assess(parse_apksigner_certs(DEBUG_OUTPUT), CAPTURED_SHA256)
        self.assertFalse(verdict.ok)
        self.assertTrue(
            any("debug" in reason.lower() for reason in verdict.reasons),
            "a debug-signed release APK must be reported as a debug fallback, "
            "not merely as a fingerprint mismatch: {0}".format(verdict.reasons))

    def test_an_unsigned_apk_fails(self):
        verdict = assess([], CAPTURED_SHA256)
        self.assertFalse(verdict.ok)
        self.assertTrue(any("unsigned" in reason.lower() for reason in verdict.reasons))

    def test_an_extra_signer_fails_even_when_the_release_key_is_present(self):
        verdict = assess(parse_apksigner_certs(TWO_SIGNER_OUTPUT), CAPTURED_SHA256)
        self.assertFalse(verdict.ok)

    def test_the_expected_fingerprint_may_be_given_in_keytool_form(self):
        verdict = assess(
            parse_apksigner_certs(CAPTURED_VERBOSE_OUTPUT), CAPTURED_KEYTOOL_FORM)
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
