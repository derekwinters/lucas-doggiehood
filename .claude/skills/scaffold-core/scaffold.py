#!/usr/bin/env python3
"""Scaffold a Doggiehood.Core class + NUnit test file (see SKILL.md).

Usage: python3 .claude/skills/scaffold-core/scaffold.py <Area> <Name>

Creates class, test, and Unity .meta files following project conventions.
Refuses to overwrite existing files. Override the repo root with
DOGGIEHOOD_ROOT (useful for dry runs).
"""
import os
import re
import sys
import uuid

FOLDER_META = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MONO_META = """fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

CLASS_TEMPLATE = """namespace Doggiehood.Core.{area}
{{
    public class {name}
    {{
        // Intentionally empty: write the failing test first
        // (CoreTests/Doggiehood.Core.Tests/{area}/{name}Tests.cs),
        // show it red, then implement the minimum here.
    }}
}}
"""

TEST_TEMPLATE = """using Doggiehood.Core.{area};
using NUnit.Framework;

namespace Doggiehood.Core.Tests.{area}
{{
    public class {name}Tests
    {{
        [Test]
        public void ReplaceMe_WithTheFirstRealBehaviorAssertion()
        {{
            // Placeholder from scaffold-core: fails on purpose so the suite
            // is red until the first real test replaces it.
            Assert.Fail("Write the first real test for {name}.");
        }}
    }}
}}
"""


def die(msg):
    print("scaffold-core: " + msg, file=sys.stderr)
    sys.exit(1)


def write_new(path, content):
    if os.path.exists(path):
        die(path + " already exists; refusing to overwrite.")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        f.write(content)
    print("created " + path)


def main():
    if len(sys.argv) != 3:
        die("usage: scaffold.py <Area> <Name>  (e.g. scaffold.py Economy CoinWallet)")
    area, name = sys.argv[1], sys.argv[2]
    ident = r"^[A-Z][A-Za-z0-9]*$"
    if not re.match(ident, area) or not re.match(ident, name):
        die("Area and Name must be PascalCase identifiers.")

    root = os.environ.get("DOGGIEHOOD_ROOT", ".")
    core_dir = os.path.join(root, "Assets", "Scripts", "Core", area)
    class_path = os.path.join(core_dir, name + ".cs")
    test_path = os.path.join(
        root, "CoreTests", "Doggiehood.Core.Tests", area, name + "Tests.cs"
    )

    area_is_new = not os.path.isdir(core_dir)
    write_new(class_path, CLASS_TEMPLATE.format(area=area, name=name))
    write_new(class_path + ".meta", MONO_META.format(g=uuid.uuid4().hex))
    if area_is_new:
        write_new(core_dir + ".meta", FOLDER_META.format(g=uuid.uuid4().hex))
    write_new(test_path, TEST_TEMPLATE.format(area=area, name=name))

    print()
    print("Next (strict TDD):")
    print("  1. Put the first real assertion in " + test_path)
    print("  2. dotnet test CoreTests/Doggiehood.Core.Tests   # show red")
    print("  3. Implement the minimum in " + class_path + "   # show green")


if __name__ == "__main__":
    main()
