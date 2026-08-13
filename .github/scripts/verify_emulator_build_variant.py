#!/usr/bin/env python3
"""Release gate: the emulator APK must actually *be* the emulator build (#706).

**Why this exists.** `v0.14.0` shipped `doggiehood-v0.14.0-emulator.apk` as a
byte-for-byte copy of the device APK — identical SHA-256, the bare
`com.derekwinters.doggiehood` applicationId, `arm64-v8a` native libs. None of
`EmulatorBuildProcessor`'s mutations reached it: both `unity-builder` steps in
`build-and-attach` share one `Library`, and the emulator step was served from
the device build's incremental player cache (`*** Tundra build success (0.13
seconds), 0 items updated` / `player data was not rebuilt`). The job went green
and uploaded the wrong file, and nobody could tell until a field ANR report
months later showed the unsuffixed process name.

**The invariant this enforces.** *The emulator release asset is never a copy of
the device APK, and the device asset never carries the emulator profile.* Both
directions matter: the first is the v0.14.0 defect; the second is what a broken
`RestoreIfApplied` would look like, and it would ship the emulator profile to
real phones.

Run against the two just-built APKs *before* either is uploaded, so a build that
failed to differentiate fails the release job instead of reaching the release
page.

The decisions are pure functions so they can be unit-tested without a Unity
checkout or a real build: `read_manifest_package` (binary AndroidManifest.xml →
applicationId), `read_apk_facts` (APK → the facts we judge), and `assess` (the
pass/fail call). `main` wires them to the filesystem.
"""

import argparse
import hashlib
import os
import struct
import sys
import zipfile
from collections import namedtuple

# --- What a correct release pair looks like ---------------------------------

# EmulatorBuildProfile.Apply appends this to the applicationId for the emulator
# variant only (Assets/Scripts/Core/Versioning/EmulatorBuildProfile.cs).
EMULATOR_APPLICATION_ID_SUFFIX = ".emulator"

# EmulatorBuildProcessor pins the emulator variant to x86_64 only; the committed
# ProjectSettings default (and so the device APK) is ARM64 only.
EMULATOR_ABIS = frozenset({"x86_64"})
DEVICE_ABIS = frozenset({"arm64-v8a"})

MANIFEST_ENTRY = "AndroidManifest.xml"
NATIVE_LIB_PREFIX = "lib/"

# --- Binary XML (AXML) constants, per AOSP androidfw/ResourceTypes.h --------

RES_XML_TYPE = 0x0003
RES_STRING_POOL_TYPE = 0x0001
RES_XML_START_ELEMENT_TYPE = 0x0102

STRING_POOL_UTF8_FLAG = 1 << 8

# Res_value.dataType for a plain string reference into the pool.
TYPE_STRING = 0x03

# 0xFFFFFFFF is AXML's "no such string" / "no namespace" sentinel.
NO_REFERENCE = 0xFFFFFFFF

CHUNK_HEADER_SIZE = 8
STRING_POOL_HEADER_SIZE = 28
ATTRIBUTE_SIZE = 20

MANIFEST_ELEMENT = "manifest"
PACKAGE_ATTRIBUTE = "package"

ApkFacts = namedtuple("ApkFacts", ("path", "digest", "package", "abis"))
Verdict = namedtuple("Verdict", ("ok", "reasons"))


class MalformedManifest(Exception):
    """Raised when an APK's AndroidManifest.xml can't be read as binary XML."""


# --- Binary AndroidManifest.xml -> applicationId ----------------------------


def _decode_pool_string(data, position, utf8):
    """Decode one length-prefixed string from a string pool."""
    if utf8:
        # Two independently length-prefixed counts: UTF-16 length, then the
        # byte length of the UTF-8 payload. Either may use the 2-byte form.
        position, _ = _read_varint8(data, position)
        position, byte_length = _read_varint8(data, position)
        return data[position:position + byte_length].decode("utf-8", "replace")

    length = struct.unpack_from("<H", data, position)[0]
    position += 2
    if length & 0x8000:
        low = struct.unpack_from("<H", data, position)[0]
        position += 2
        length = ((length & 0x7FFF) << 16) | low
    return data[position:position + length * 2].decode("utf-16-le", "replace")


def _read_varint8(data, position):
    """Read AXML's 1-or-2-byte length prefix used by UTF-8 string pools."""
    value = data[position]
    position += 1
    if value & 0x80:
        value = ((value & 0x7F) << 8) | data[position]
        position += 1
    return position, value


def _parse_string_pool(data, offset):
    header_size = struct.unpack_from("<H", data, offset + 2)[0]
    string_count, _style_count, flags, strings_start, _styles_start = struct.unpack_from(
        "<IIIII", data, offset + CHUNK_HEADER_SIZE
    )
    utf8 = bool(flags & STRING_POOL_UTF8_FLAG)

    index_base = offset + max(header_size, STRING_POOL_HEADER_SIZE)
    offsets = struct.unpack_from("<{0}I".format(string_count), data, index_base)
    pool_base = offset + strings_start
    return [_decode_pool_string(data, pool_base + o, utf8) for o in offsets]


def _resolve(strings, index):
    if index == NO_REFERENCE or index >= len(strings):
        return None
    return strings[index]


def _package_from_start_element(data, offset, strings):
    """Return the `package` attribute of a <manifest> start element, else None."""
    header_size = struct.unpack_from("<H", data, offset + 2)[0]
    ext = offset + header_size

    _ns, name_index = struct.unpack_from("<II", data, ext)
    if _resolve(strings, name_index) != MANIFEST_ELEMENT:
        return None

    attribute_start, attribute_size, attribute_count = struct.unpack_from(
        "<HHH", data, ext + 8
    )
    attribute_size = attribute_size or ATTRIBUTE_SIZE

    for i in range(attribute_count):
        attribute = ext + attribute_start + i * attribute_size
        _attr_ns, attr_name, raw_value = struct.unpack_from("<III", data, attribute)
        if _resolve(strings, attr_name) != PACKAGE_ATTRIBUTE:
            continue

        package = _resolve(strings, raw_value)
        if package is not None:
            return package

        # No raw value retained — fall back to the typed value, which for an
        # applicationId is a string reference into the same pool.
        _size, _res0, data_type, value = struct.unpack_from("<HBBI", data, attribute + 12)
        if data_type == TYPE_STRING:
            return _resolve(strings, value)

    return None


def read_manifest_package(axml):
    """Extract the applicationId from a binary AndroidManifest.xml.

    Raises `MalformedManifest` if the bytes are not binary XML, are truncated,
    or carry no `<manifest package="...">` — an unreadable manifest is never
    treated as a pass.
    """
    try:
        if len(axml) < CHUNK_HEADER_SIZE:
            raise MalformedManifest("AndroidManifest.xml is too short to be binary XML")

        magic, _header_size, _size = struct.unpack_from("<HHI", axml, 0)
        if magic != RES_XML_TYPE:
            raise MalformedManifest(
                "AndroidManifest.xml is not binary XML (chunk type 0x{0:04x})".format(magic)
            )

        strings = []
        offset = CHUNK_HEADER_SIZE
        while offset + CHUNK_HEADER_SIZE <= len(axml):
            chunk_type, _chunk_header, chunk_size = struct.unpack_from("<HHI", axml, offset)
            if chunk_size <= 0:
                break

            if chunk_type == RES_STRING_POOL_TYPE:
                strings = _parse_string_pool(axml, offset)
            elif chunk_type == RES_XML_START_ELEMENT_TYPE:
                package = _package_from_start_element(axml, offset, strings)
                if package:
                    return package

            offset += chunk_size
    except MalformedManifest:
        raise
    except (struct.error, IndexError, ValueError) as exc:
        raise MalformedManifest("AndroidManifest.xml is malformed: {0}".format(exc))

    raise MalformedManifest('no <manifest package="..."> found in AndroidManifest.xml')


# --- APK -> facts -----------------------------------------------------------


def read_apk_facts(path):
    """Read the applicationId, native-lib ABIs and SHA-256 of one APK."""
    with open(path, "rb") as handle:
        digest = hashlib.sha256(handle.read()).hexdigest()

    try:
        with zipfile.ZipFile(path) as apk:
            names = apk.namelist()
            if MANIFEST_ENTRY not in names:
                raise MalformedManifest("{0} contains no {1}".format(path, MANIFEST_ENTRY))
            package = read_manifest_package(apk.read(MANIFEST_ENTRY))
    except zipfile.BadZipFile as exc:
        raise MalformedManifest("{0} is not a readable APK: {1}".format(path, exc))

    abis = frozenset(
        name.split("/")[1]
        for name in names
        if name.startswith(NATIVE_LIB_PREFIX) and name.count("/") > 1
    )
    return ApkFacts(path=path, digest=digest, package=package, abis=abis)


# --- The verdict ------------------------------------------------------------


def assess(device, emulator):
    """Decide whether a device/emulator APK pair is a correctly differentiated build.

    Returns a `Verdict`; `reasons` is empty exactly when the pair is good. Every
    independent problem is reported, so one release run surfaces all of them
    rather than one per re-run.
    """
    reasons = []

    if device.digest == emulator.digest:
        reasons.append(
            "the two APKs are byte-for-byte identical (sha256 {0}) — the emulator "
            "build produced no distinct artifact".format(device.digest)
        )

    if not emulator.package.endswith(EMULATOR_APPLICATION_ID_SUFFIX):
        reasons.append(
            "emulator APK applicationId is '{0}', expected one ending in '{1}'".format(
                emulator.package, EMULATOR_APPLICATION_ID_SUFFIX
            )
        )

    if device.package.endswith(EMULATOR_APPLICATION_ID_SUFFIX):
        reasons.append(
            "device APK applicationId is '{0}' — the emulator profile leaked into "
            "the device build".format(device.package)
        )

    if emulator.abis != EMULATOR_ABIS:
        reasons.append(
            "emulator APK native libs are {0}, expected exactly {1}".format(
                _format_abis(emulator.abis), _format_abis(EMULATOR_ABIS)
            )
        )

    if device.abis != DEVICE_ABIS:
        reasons.append(
            "device APK native libs are {0}, expected exactly {1}".format(
                _format_abis(device.abis), _format_abis(DEVICE_ABIS)
            )
        )

    return Verdict(ok=not reasons, reasons=reasons)


def _format_abis(abis):
    return "{" + ", ".join(sorted(abis)) + "}" if abis else "{none}"


# --- Wiring -----------------------------------------------------------------


def _describe(label, facts):
    return "{0}: {1}\n  applicationId: {2}\n  native libs:   {3}\n  sha256:        {4}".format(
        label, os.path.basename(facts.path), facts.package, _format_abis(facts.abis), facts.digest
    )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("device_apk", help="the device release APK")
    parser.add_argument("emulator_apk", help="the emulator-targeted release APK")
    args = parser.parse_args(argv)

    try:
        device = read_apk_facts(args.device_apk)
        emulator = read_apk_facts(args.emulator_apk)
    except (MalformedManifest, OSError) as exc:
        print("::error title=Emulator build check::{0}".format(exc))
        return 1

    print(_describe("device  ", device))
    print(_describe("emulator", emulator))

    verdict = assess(device, emulator)
    if verdict.ok:
        print("\nOK: the emulator APK is a genuinely distinct emulator build.")
        return 0

    for reason in verdict.reasons:
        print("::error title=Emulator build check::{0}".format(reason))
    print(
        "\nThe emulator release asset is not a real emulator build — refusing to "
        "upload it. See issue #706 and docs/engineering/ci-cd.md."
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
