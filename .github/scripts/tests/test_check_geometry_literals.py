"""Unit tests for the geometry/tuning-literal check (issue #161).

The rule (docs/engineering/tech-stack.md): every geometry/layout/tuning value —
sizes, offsets, margins, positions, durations, speeds, payouts — is a named
constant/static/serialized field at the top of its type, never an inline
numeric literal in a method body.

`check_geometry_literals.py` is a deliberately conservative backstop for the
egregious case that motivated the rule (#159: `140f`/`16f`/`32f` inside
`OnGUI`). It flags f-suffixed floating-point literals of magnitude >=
`FLAG_MIN_MAGNITUDE` that sit in an executable (method-body) context, and
ratchets against a committed baseline so pre-existing literals don't block CI
while newly introduced ones do.

These tests pin the two pure functions the check is built from: `scan_source`
(what counts as a violation) and `diff_baseline` (the ratchet).
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from check_geometry_literals import (  # noqa: E402
    FLAG_MIN_MAGNITUDE,
    diff_baseline,
    key_for,
    scan_source,
)


def _values(findings):
    return sorted(f.value for f in findings)


class ScanSourceTests(unittest.TestCase):
    def test_flags_magic_literal_in_method_body(self):
        src = """
namespace N {
    class C {
        void M() {
            var w = 140f;
        }
    }
}
"""
        findings = scan_source(src)
        self.assertEqual(_values(findings), [140.0])
        self.assertEqual(findings[0].literal, "140f")
        self.assertIn("140f", findings[0].snippet)

    def test_flags_multiple_literals_on_one_line(self):
        src = """
class C {
    void M() {
        var r = new Rect(140f, 16f, 32f, 90f);
    }
}
"""
        self.assertEqual(_values(scan_source(src)), [16.0, 32.0, 90.0, 140.0])

    def test_does_not_flag_named_constant_declaration(self):
        # The literal lives in a type-level const/field declaration — this is
        # exactly the compliant form the rule wants, so it must never flag.
        src = """
class C {
    public const float HeightPx = 88f;
    private static readonly float Margin = 36f;
    [SerializeField] private float speed = 50f;
    void M() {
        var h = HeightPx;
    }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_does_not_flag_structural_small_values(self):
        # 0/1/2 (identity, both-sides, centering) and sub-threshold fractions
        # (anchors, colour channels, epsilons) are treated as structural.
        src = """
class C {
    void M() {
        var a = size / 2f;
        var b = 2f * margin;
        var c = new Vector2(0f, 0.5f);
        var col = new Color(0.9f, 0.15f, 0.15f);
        if (delta > 0.0001f) { }
    }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_flags_local_variable_holding_magic_value(self):
        # A local is not "a named field at the top of its type", so a magic
        # value assigned to a local is still a violation.
        src = """
class C {
    void M() {
        var pinchSpeed = 50f;
    }
}
"""
        self.assertEqual(_values(scan_source(src)), [50.0])

    def test_ignores_literals_in_comments(self):
        src = """
class C {
    void M() {
        // historically this was 140f in OnGUI
        var x = HeightPx; /* was 32f */
    }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_ignores_literals_in_strings(self):
        src = """
class C {
    void M() {
        var s = "sized at 140f wide";
        var t = $"speed {name} = 90f";
    }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_does_not_match_f_inside_identifier_or_hex(self):
        # `Vector3f`-style identifiers and hex byte channels must not match.
        src = """
class C {
    void M() {
        var col = new Color32(0x2E, 0x2A, 0xFF, 0xFF);
        transform.position = readValue3f();
    }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_flags_literal_on_same_line_as_method_signature(self):
        # The method body shares the signature's line, nested inside a class —
        # context must be resolved at the literal's position, not per whole line.
        src = """
namespace N {
    class C {
        void M() { var w = 777f; }
    }
}
"""
        self.assertEqual(_values(scan_source(src)), [777.0])

    def test_does_not_flag_member_collection_initializer(self):
        # A type-level field initialized with a braced collection literal is
        # still the compliant named form, even though the value sits after `{`.
        src = """
class C {
    static readonly float[] Speeds = { 50f, 90f };
    void M() { var s = Speeds; }
}
"""
        self.assertEqual(scan_source(src), [])

    def test_flag_min_magnitude_boundary(self):
        # Exactly at the threshold flags; just below is structural.
        below = "class C { void M() { var x = 2.9f; } }"
        at = "class C { void M() { var x = 3f; } }"
        self.assertEqual(scan_source(below), [])
        self.assertEqual(_values(scan_source(at)), [FLAG_MIN_MAGNITUDE])


class DiffBaselineTests(unittest.TestCase):
    def test_new_violation_absent_from_baseline_is_reported(self):
        current = {"a.cs\t90f\tvar x = 90f;"}
        baseline = set()
        new, fixed = diff_baseline(current, baseline)
        self.assertEqual(new, ["a.cs\t90f\tvar x = 90f;"])
        self.assertEqual(fixed, [])

    def test_baselined_violation_is_not_reported(self):
        key = "a.cs\t90f\tvar x = 90f;"
        new, fixed = diff_baseline({key}, {key})
        self.assertEqual(new, [])
        self.assertEqual(fixed, [])

    def test_removed_baseline_entry_is_reported_as_fixed_not_new(self):
        # A refactor that removes a baselined literal must not fail the gate;
        # it is surfaced as "fixed" so the baseline can be pruned.
        key = "a.cs\t90f\tvar x = 90f;"
        new, fixed = diff_baseline(set(), {key})
        self.assertEqual(new, [])
        self.assertEqual(fixed, [key])

    def test_key_for_is_line_number_independent(self):
        # Two identical snippets at different lines produce the same key, so
        # the baseline survives line shifts elsewhere in the file.
        finding_a = scan_source("class C { void M() { var x = 90f; } }")[0]
        k1 = key_for("a.cs", finding_a)
        finding_b = scan_source("\n\n\nclass C { void M() { var x = 90f; } }")[0]
        k2 = key_for("a.cs", finding_b)
        self.assertEqual(k1, k2)


if __name__ == "__main__":
    unittest.main()
