#!/usr/bin/env python3
"""Build gate: a debug APK must actually carry the `.debug` applicationId (#734).

**Why this exists.** `pr-build.yml` and `rc-build.yml` asked for the suffix with
a step `env:` entry, `DOGGIEHOOD_DEBUG_BUILD: "true"`, and
`DebugApplicationIdBuildProcessor` read it with `Environment.GetEnvironmentVariable`.
But `game-ci/unity-builder` does not run Unity in the step's process — it runs it
inside a Docker container and forwards only a fixed allowlist of variables
(`UNITY_*`, `BUILD_*`, `ANDROID_*`, `CUSTOM_PARAMETERS`, `GITHUB_*`, `RUNNER_*`).
The variable was set on the runner and never inside the container, so the hook
no-op'd and every PR and RC APK shipped the bare `com.derekwinters.doggiehood`
id. Installed alongside a release build it would have *replaced* it instead of
sitting beside it — the one thing the suffix exists to prevent. It survived
because, unlike the emulator variant (#706/#731), nothing ever checked.

**The invariant this enforces.** *A build that asks for the debug suffix proves
it applied before its artifact is uploaded.* Run against the just-built APK
*before* `actions/upload-artifact`, so a silent no-op fails the job instead of
producing a mislabelled APK.

The applicationId is read with the **same** binary-AndroidManifest parser the
emulator release gate uses — imported, not copied, so there is exactly one such
parser in the repo and it stays the one proven against a real shipped manifest.
`assess` (the pass/fail call) is a pure function; `main` wires it to the
filesystem.
"""

import argparse
import os
import sys
from collections import namedtuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from verify_emulator_build_variant import (  # noqa: E402
    ApkFacts,
    MalformedManifest,
    read_apk_facts,
    read_manifest_package,
)

# --- What a correct debug artifact looks like -------------------------------

# ApplicationIdSuffix.Apply appends this for debug builds only
# (Assets/Scripts/Core/Versioning/ApplicationIdSuffix.cs). The permanent id is
# the release one, unchanged (#80).
PERMANENT_APPLICATION_ID = "com.derekwinters.doggiehood"
DEBUG_APPLICATION_ID_SUFFIX = ".debug"

EXPECTED_APPLICATION_ID = PERMANENT_APPLICATION_ID + DEBUG_APPLICATION_ID_SUFFIX

Verdict = namedtuple("Verdict", ("ok", "reasons"))

__all__ = [
    "ApkFacts",
    "DEBUG_APPLICATION_ID_SUFFIX",
    "EXPECTED_APPLICATION_ID",
    "MalformedManifest",
    "PERMANENT_APPLICATION_ID",
    "Verdict",
    "assess",
    "main",
    "read_apk_facts",
    "read_manifest_package",
]


# --- The verdict ------------------------------------------------------------


def assess(debug_apk):
    """Decide whether a debug APK really carries the debug applicationId.

    Returns a `Verdict`; `reasons` is empty exactly when the artifact is good.
    The check is an equality against the one id a debug build may ship, not a
    bare `endswith`: a compounded `.debug.debug` (a restore that stopped
    working) or an id from another project is no more installable alongside a
    release build than the unsuffixed one is.
    """
    if debug_apk.package == EXPECTED_APPLICATION_ID:
        return Verdict(ok=True, reasons=[])

    if debug_apk.package == PERMANENT_APPLICATION_ID:
        return Verdict(ok=False, reasons=[
            "debug APK applicationId is the permanent release id '{0}' — the "
            "'{1}' suffix never applied, so this build would replace a release "
            "install instead of sitting beside it".format(
                debug_apk.package, DEBUG_APPLICATION_ID_SUFFIX
            )
        ])

    return Verdict(ok=False, reasons=[
        "debug APK applicationId is '{0}', expected exactly '{1}'".format(
            debug_apk.package, EXPECTED_APPLICATION_ID
        )
    ])


# --- Wiring -----------------------------------------------------------------


def _describe(facts):
    return "debug APK: {0}\n  applicationId: {1}\n  sha256:        {2}".format(
        os.path.basename(facts.path), facts.package, facts.digest
    )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("debug_apk", help="the just-built PR or RC debug APK")
    args = parser.parse_args(argv)

    try:
        facts = read_apk_facts(args.debug_apk)
    except (MalformedManifest, OSError) as exc:
        print("::error title=Debug applicationId check::{0}".format(exc))
        return 1

    print(_describe(facts))

    verdict = assess(facts)
    if verdict.ok:
        print("\nOK: the debug APK carries the '{0}' applicationId suffix.".format(
            DEBUG_APPLICATION_ID_SUFFIX))
        return 0

    for reason in verdict.reasons:
        print("::error title=Debug applicationId check::{0}".format(reason))
    print(
        "\nThis artifact is not a real debug build — refusing to upload it. "
        "See issue #734 and docs/engineering/ci-cd.md."
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
