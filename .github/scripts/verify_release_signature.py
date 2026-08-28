#!/usr/bin/env python3
"""Release gate: every release APK is signed by the one stable release key (#630).

**Why this exists.** Every build before #630 used Android's *default debug
signing*, deferred deliberately in #75. A debug key is not a stable owned key,
so a new APK can fail to install over an installed one with a signature
mismatch — and Android's only remedy is uninstall-then-reinstall, which wipes
`doggiehood-save.txt`. The game is offline and local-save-only
(`docs/specs/product-scope.md`), so that save is the only copy in existence.
This is the delivery half of save stability (#628): a frozen save schema is
moot if the upgrade path deletes the save.

**The invariant this enforces.** *A published release asset is signed by the
release certificate pinned in `.github/release-cert-sha256.txt`, and by no
other key.* The failure it is really built for is the quiet one: if a keystore
input is mis-wired, or a secret is renamed, or the workflow is copied without
it, Unity does not fail — it falls back to the debug key and produces a
perfectly good APK that can never be upgraded in place. That APK looks correct
in every other respect, so nothing but the certificate distinguishes it.

Run against each just-built APK *before* anything is uploaded, so a build that
lost the release key fails the release job instead of reaching the release page
— where, once someone installs it, the damage cannot be undone by re-uploading
a correct asset.

The certificate fingerprint is *public* — it ships inside every APK — which is
why it is committed rather than held as a secret. The keystore and its
passwords are the secrets (#629).

The decisions are pure functions so they unit-test without an Android SDK or a
real build: `normalize_fingerprint`, `parse_apksigner_certs`, and `assess`.
`main` wires them to apksigner and the filesystem.
"""

import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
from collections import namedtuple

SignerFacts = namedtuple("SignerFacts", "index dn sha256")
Verdict = namedtuple("Verdict", "ok reasons")

# The default Android debug certificate's subject. Unity signs with this key
# whenever no keystore is configured, so it is the signature of the exact
# mis-wiring this gate exists to catch — worth naming in the error rather than
# reporting as an anonymous fingerprint mismatch.
ANDROID_DEBUG_DN_MARKER = "cn=android debug"

EXPECTED_FINGERPRINT_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "release-cert-sha256.txt")

_SHA256_HEX_DIGITS = 64

# apksigner prints "Signer #1 certificate DN: ..." and, when signers differ per
# SDK range, "Signer (minSdkVersion=24, maxSdkVersion=32) #1 certificate DN:".
# Both shapes carry the signer index, which is what identifies the block.
_SIGNER_LINE = re.compile(
    r"^Signer\s*(?:\([^)]*\)\s*)?#(\d+)\s+certificate\s+(DN|SHA-256 digest):\s*(.*)$")
_SIGNER_COUNT_LINE = re.compile(r"^Number of signers:\s*(\d+)\s*$")

_FINGERPRINT_LABEL = re.compile(r"^\s*SHA-?256\s*:", re.IGNORECASE)


class MalformedFingerprint(ValueError):
    """A certificate fingerprint that is not 32 hex-encoded bytes."""


class MalformedApksignerOutput(ValueError):
    """apksigner output this parser cannot read with confidence."""


# --- Pure decisions ---------------------------------------------------------


def normalize_fingerprint(text):
    """A SHA-256 certificate fingerprint as 64 lowercase hex digits.

    keytool prints `SHA256: B7:C7:...` and apksigner prints bare lowercase hex,
    but both are SHA-256 over the DER-encoded certificate — the same 32 bytes
    written two ways. Normalizing here is what makes either form safe to paste
    into `.github/release-cert-sha256.txt`.
    """
    if text is None:
        raise MalformedFingerprint("no fingerprint given")

    bare = _FINGERPRINT_LABEL.sub("", text)
    bare = re.sub(r"[\s:]", "", bare).lower()

    if not bare:
        raise MalformedFingerprint("no fingerprint given")
    if len(bare) != _SHA256_HEX_DIGITS:
        raise MalformedFingerprint(
            "expected {0} hex digits for a SHA-256 fingerprint, got {1}: {2!r}".format(
                _SHA256_HEX_DIGITS, len(bare), text.strip()))
    if not re.fullmatch(r"[0-9a-f]+", bare):
        raise MalformedFingerprint(
            "fingerprint is not hexadecimal: {0!r}".format(text.strip()))
    return bare


def parse_apksigner_certs(text):
    """The signers described by `apksigner verify --print-certs --verbose`.

    The `--verbose` half of that command line is load-bearing here: it is what
    makes apksigner emit the `Number of signers:` header this parser requires
    (#752). Read `print_certs` before changing either.

    Raises `MalformedApksignerOutput` when the declared signer count disagrees
    with the blocks actually parsed. That check is the point: this parser reads
    a human-readable format that could change under us, and a silent "no
    signers found" would read as an unsigned APK — a confusing failure — while
    a silently *short* list could let an unexamined signer through.
    """
    declared = None
    by_index = {}

    for line in text.splitlines():
        count_match = _SIGNER_COUNT_LINE.match(line.strip())
        if count_match and declared is None:
            declared = int(count_match.group(1))
            continue

        signer_match = _SIGNER_LINE.match(line.strip())
        if not signer_match:
            continue

        index = int(signer_match.group(1))
        field = signer_match.group(2)
        value = signer_match.group(3).strip()
        signer = by_index.setdefault(index, {"dn": "", "sha256": ""})

        if field == "DN":
            signer["dn"] = value
        else:
            digest = normalize_fingerprint(value)
            if signer["sha256"] and signer["sha256"] != digest:
                raise MalformedApksignerOutput(
                    "signer #{0} is reported with two different certificates, "
                    "{1} and {2}".format(index, signer["sha256"], digest))
            signer["sha256"] = digest

    if declared is None:
        raise MalformedApksignerOutput(
            "apksigner output has no 'Number of signers:' line — the format this "
            "gate reads has changed, so its verdict cannot be trusted")

    if declared != len(by_index):
        raise MalformedApksignerOutput(
            "apksigner reported {0} signer(s) but {1} could be parsed — the format "
            "this gate reads has changed".format(declared, len(by_index)))

    return [
        SignerFacts(index=index, dn=by_index[index]["dn"], sha256=by_index[index]["sha256"])
        for index in sorted(by_index)
    ]


def assess(signers, expected_sha256):
    """Whether these signers are exactly the pinned release certificate."""
    expected = normalize_fingerprint(expected_sha256)
    reasons = []

    if not signers:
        reasons.append(
            "the APK is unsigned — a release asset must carry the release "
            "certificate {0}".format(expected))
        return Verdict(ok=False, reasons=reasons)

    if len(signers) != 1:
        reasons.append(
            "the APK has {0} signers; a release asset is signed by the release key "
            "alone".format(len(signers)))

    for signer in signers:
        if signer.sha256 == expected:
            continue
        if ANDROID_DEBUG_DN_MARKER in signer.dn.lower():
            reasons.append(
                "signer #{0} is the Android debug certificate ({1}) — the build fell "
                "back to debug signing, so this APK can never be installed over a "
                "release build. The keystore inputs did not reach Unity.".format(
                    signer.index, signer.dn))
        else:
            reasons.append(
                "signer #{0} certificate is {1}, expected the release certificate "
                "{2} (DN: {3})".format(signer.index, signer.sha256, expected, signer.dn))

    return Verdict(ok=not reasons, reasons=reasons)


def read_expected_fingerprint(path):
    """The pinned release fingerprint, ignoring blank and `#` comment lines."""
    with open(path, encoding="utf-8") as handle:
        lines = [
            line.strip() for line in handle
            if line.strip() and not line.strip().startswith("#")
        ]
    if len(lines) != 1:
        raise MalformedFingerprint(
            "{0} must contain exactly one fingerprint, found {1}".format(path, len(lines)))
    return normalize_fingerprint(lines[0])


# --- Wiring -----------------------------------------------------------------


def find_apksigner():
    """apksigner from PATH, else the newest build-tools copy in the SDK."""
    on_path = shutil.which("apksigner")
    if on_path:
        return on_path

    for root in (os.environ.get("ANDROID_HOME"), os.environ.get("ANDROID_SDK_ROOT")):
        if not root:
            continue
        candidates = sorted(glob.glob(os.path.join(root, "build-tools", "*", "apksigner")))
        if candidates:
            return candidates[-1]

    raise OSError(
        "apksigner not found on PATH or in ANDROID_HOME/ANDROID_SDK_ROOT build-tools")


def print_certs(apk, apksigner=None):
    """`apksigner verify --print-certs --verbose` output for one APK.

    `--verbose` is not decoration: apksigner prints the `Number of signers: N`
    header (along with `Verifies` and the `Verified using vN scheme` lines)
    only in verbose mode, while `--print-certs` alone emits the signer DN and
    SHA-256 blocks and nothing else. `parse_apksigner_certs` cross-checks that
    header against the blocks it parsed, so without the flag every real APK is
    rejected as malformed output — which is exactly what happened to the
    v0.16.0 release, shipped with no assets at all (#752).
    """
    tool = apksigner or find_apksigner()
    result = subprocess.run(
        [tool, "verify", "--print-certs", "--verbose", apk],
        capture_output=True, text=True, check=False)
    if result.returncode != 0:
        raise MalformedApksignerOutput(
            "apksigner could not verify {0} (exit {1}): {2}".format(
                os.path.basename(apk), result.returncode,
                (result.stderr or result.stdout).strip()))
    return result.stdout


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("apk", nargs="+", help="release APK(s) to check")
    parser.add_argument(
        "--expected-file", default=EXPECTED_FINGERPRINT_FILE,
        help="file holding the pinned release certificate fingerprint")
    parser.add_argument(
        "--expected", default=None,
        help="the pinned fingerprint itself, overriding --expected-file")
    args = parser.parse_args(argv)

    try:
        expected = (
            normalize_fingerprint(args.expected) if args.expected
            else read_expected_fingerprint(args.expected_file))
    except (MalformedFingerprint, OSError) as exc:
        print("::error title=Release signature::{0}".format(exc))
        return 1

    failed = False
    for apk in args.apk:
        name = os.path.basename(apk)
        try:
            signers = parse_apksigner_certs(print_certs(apk))
        except (MalformedApksignerOutput, MalformedFingerprint, OSError) as exc:
            print("::error title=Release signature::{0}: {1}".format(name, exc))
            failed = True
            continue

        for signer in signers:
            print("{0}: signer #{1} {2}\n  DN: {3}".format(
                name, signer.index, signer.sha256, signer.dn))

        verdict = assess(signers, expected)
        if verdict.ok:
            print("OK: {0} is signed by the release certificate.".format(name))
            continue

        failed = True
        for reason in verdict.reasons:
            print("::error title=Release signature::{0}: {1}".format(name, reason))

    if failed:
        print(
            "\nA release asset is not signed by the pinned release certificate — "
            "refusing to upload it. Installing it would force an uninstall on every "
            "player's next upgrade, wiping their save. See issue #630 and "
            "docs/engineering/ci-cd.md.")
        return 1

    print("\nOK: every release asset carries the pinned release certificate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
