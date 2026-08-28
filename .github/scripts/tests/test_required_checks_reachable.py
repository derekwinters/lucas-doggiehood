"""The `main` ruleset's required checks must be able to run on every PR (#751).

`main`'s branch ruleset requires six status checks before a PR may merge. Two of
the workflows producing them were path-filtered to game code, so on a PR that
touched none of those paths the workflow never triggered, never created a check
run, and the ruleset waited forever for a result that could not arrive. Release
PRs (`VERSION`/`CHANGELOG.md`/the release-please manifest) were unmergeable that
way from the day the ruleset was created, and so was any PR confined to
`.github/**` or `docs/**`.

Requiring a context and path-filtering it out of a PR class are contradictory
settings, and the contradiction is invisible: nothing fails, the PR simply never
becomes mergeable. Nothing could notice it either, because a workflow's path
filter cannot see the ruleset it has to stay in sync with. So this test holds
both halves in one place:

* the six required contexts are hard-coded here (no network — the ruleset is not
  readable from a test run), mapped to the workflow job that produces each;
* the workflows' `pull_request` triggers are parsed out of the YAML and checked
  for reachability against a representative file set for every class of PR this
  repo actually produces.

The fix these tests pin is *no* `pull_request` path filter on a
required-context workflow, rather than a longer allowlist. Every filter has a PR
class it excludes — an allowlist excludes whatever it forgot, and a
`paths-ignore` excludes any PR confined to the ignored set — and the excluded
class deadlocks. `docs-test.yml` has run unfiltered on every PR for exactly this
reason since long before #751.
"""

import os
import re
import unittest

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))))
WORKFLOWS = os.path.join(REPO_ROOT, ".github", "workflows")

ISSUE = "#751"

# The six contexts the `main` ruleset lists as required, each mapped to the
# workflow file and job id that produces it. Hard-coded deliberately: the
# ruleset lives in repo settings, not in the tree, so this table IS the
# in-repo record of it. If a context is added to or renamed in the ruleset,
# update it here — that edit is the moment to check reachability.
REQUIRED_CONTEXTS = {
    "Conventional Commits PR title": ("pr-title-lint.yml", "lint"),
    "Debug APK": ("pr-build.yml", "build-debug-apk"),
    "Release-candidate APK": ("rc-build.yml", "build-rc-apk"),
    "build": ("docs-test.yml", "build"),
    "Core NUnit tests (no Unity)": ("ci-tests.yml", "core-tests"),
    "Unity EditMode tests (headless)": ("ci-tests.yml", "editmode-tests"),
}

# One representative file set per class of PR this repo produces. Each set is
# the *complete* diff of such a PR, so a filter that matches none of its
# entries would leave that PR permanently unmergeable.
PR_CLASSES = {
    "app-code": [
        "Assets/Scripts/Core/Quests/QuestBoard.cs",
        "CoreTests/Doggiehood.Core.Tests/Quests/QuestBoardTests.cs",
    ],
    "workflow/script-only": [
        ".github/workflows/release-build.yml",
        ".github/scripts/verify_release_signature.py",
    ],
    "docs-only": [
        "docs/engineering/ci-cd.md",
        "mkdocs.yml",
    ],
    "release-please": [
        "VERSION",
        "CHANGELOG.md",
        ".github/release-please/manifest.json",
    ],
}

# The diff that actually deadlocked: #752's fix touched a CI script and the page
# documenting it, and matched neither `ci-tests.yml`'s nor `pr-build.yml`'s
# filter. Kept as its own case so the original bug has a named regression test.
DEADLOCKED_PR = [
    ".github/scripts/verify_release_signature.py",
    "docs/engineering/ci-cd.md",
]


def _glob_to_regex(pattern):
    """Compile one GitHub path-filter glob.

    `**` spans directory separators, `*` and `?` do not — the subset of
    filter syntax this repo's workflows use.
    """
    out = []
    index = 0
    while index < len(pattern):
        if pattern.startswith("**", index):
            out.append(".*")
            index += 2
        elif pattern[index] == "*":
            out.append("[^/]*")
            index += 1
        elif pattern[index] == "?":
            out.append("[^/]")
            index += 1
        else:
            out.append(re.escape(pattern[index]))
            index += 1
    return re.compile("^" + "".join(out) + "$")


def _matches(pattern, path):
    return _glob_to_regex(pattern).match(path) is not None


def _strip_comment(line):
    """Drop a trailing `# ...` comment from a line with no quoted `#`."""
    if '"' in line or "'" in line:
        return line
    return line.split("#", 1)[0]


def _scalar(text):
    text = _strip_comment(text).strip()
    if len(text) >= 2 and text[0] == text[-1] and text[0] in "\"'":
        return text[1:-1]
    return text


def _indent(line):
    return len(line) - len(line.rstrip("\n").lstrip())


def _read(workflow):
    path = os.path.join(WORKFLOWS, workflow)
    with open(path, encoding="utf-8") as handle:
        return handle.read().splitlines()


def _block(lines, start, indent):
    """The lines belonging to the block opened at `start`, by indentation."""
    body = []
    for line in lines[start + 1:]:
        if not line.strip() or line.lstrip().startswith("#"):
            body.append(line)
            continue
        if _indent(line) <= indent:
            break
        body.append(line)
    return body


def _find_key(lines, key, indent):
    """Index of `key:` at exactly `indent` spaces, or None."""
    for index, line in enumerate(lines):
        if _indent(line) != indent or line.lstrip().startswith("#"):
            continue
        stripped = line.strip()
        if stripped == key + ":" or stripped.startswith(key + ": "):
            return index
    return None


def _sequence(lines, index, indent):
    """The scalar sequence declared by the key at `index` (block or flow)."""
    inline = lines[index].split(":", 1)[1].strip()
    if inline.startswith("["):
        inner = _strip_comment(inline).strip().strip("[]")
        return [_scalar(item) for item in inner.split(",") if item.strip()]
    items = []
    for line in _block(lines, index, indent):
        stripped = line.strip()
        if stripped.startswith("- "):
            items.append(_scalar(stripped[2:]))
    return items


def trigger_filters(workflow, event):
    """The `paths` / `paths-ignore` lists of one trigger, or None if absent.

    Returns `None` when the workflow has no such trigger at all, and a dict
    (possibly empty — meaning "no path filter") when it does.
    """
    lines = _read(workflow)
    on_index = _find_key(lines, "on", 0)
    assert on_index is not None, "{0} has no top-level `on:` block".format(workflow)

    on_block = _block(lines, on_index, 0)
    event_index = _find_key(on_block, event, 2)
    if event_index is None:
        return None

    event_block = _block(on_block, event_index, 2)
    filters = {}
    for key in ("paths", "paths-ignore"):
        key_index = _find_key(event_block, key, 4)
        if key_index is not None:
            filters[key] = _sequence(event_block, key_index, 4)
    return filters


def triggers_on(filters, files):
    """Would a trigger with these filters fire for a diff of exactly `files`?"""
    allowed = filters.get("paths")
    if allowed is not None and not any(
            _matches(pattern, path) for pattern in allowed for path in files):
        return False
    ignored = filters.get("paths-ignore")
    if ignored is not None and all(
            any(_matches(pattern, path) for pattern in ignored) for path in files):
        return False
    return True


def job_context(workflow, job_id):
    """The status-check context a job reports under: its `name`, else its id."""
    lines = _read(workflow)
    jobs_index = _find_key(lines, "jobs", 0)
    assert jobs_index is not None, "{0} declares no jobs".format(workflow)

    jobs_block = _block(lines, jobs_index, 0)
    job_index = _find_key(jobs_block, job_id, 2)
    if job_index is None:
        return None

    job_block = _block(jobs_block, job_index, 2)
    steps_index = _find_key(job_block, "steps", 4)
    header = job_block if steps_index is None else job_block[:steps_index]
    name_index = _find_key(header, "name", 4)
    if name_index is None:
        return job_id
    return _scalar(header[name_index].split(":", 1)[1])


class RequiredContextMappingTests(unittest.TestCase):
    """The context names above must still be the ones the workflows report."""

    def test_every_required_context_is_produced_by_the_job_it_maps_to(self):
        for context, (workflow, job_id) in REQUIRED_CONTEXTS.items():
            with self.subTest(context=context):
                self.assertEqual(
                    context, job_context(workflow, job_id),
                    "the `main` ruleset requires the context {0!r}, but "
                    "{1}'s `{2}` job no longer reports under that name — a "
                    "renamed job is a required check that can never "
                    "report ({3})".format(context, workflow, job_id, ISSUE),
                )


class RequiredChecksAreReachableTests(unittest.TestCase):
    """The invariant: every required check can run on every PR class."""

    def test_every_required_context_triggers_on_every_pr_class(self):
        for context, (workflow, _) in REQUIRED_CONTEXTS.items():
            filters = trigger_filters(workflow, "pull_request")
            self.assertIsNotNone(
                filters,
                "{0} produces the required context {1!r} but has no "
                "`pull_request:` trigger, so it can never report on a "
                "PR ({2})".format(workflow, context, ISSUE),
            )
            for pr_class, files in PR_CLASSES.items():
                with self.subTest(context=context, pr_class=pr_class):
                    self.assertTrue(
                        triggers_on(filters, files),
                        "{0} is required on every PR, but {1}'s path filter "
                        "excludes a {2} PR ({3}), so that PR waits forever "
                        "for a check that structurally cannot "
                        "report ({4})".format(
                            context, workflow, pr_class, ", ".join(files), ISSUE),
                    )

    def test_the_pr_that_deadlocked_reaches_every_required_context(self):
        for context, (workflow, _) in REQUIRED_CONTEXTS.items():
            with self.subTest(context=context):
                filters = trigger_filters(workflow, "pull_request")
                self.assertIsNotNone(filters, workflow)
                self.assertTrue(
                    triggers_on(filters, DEADLOCKED_PR),
                    "a PR touching only {0} must reach {1!r}; this exact diff "
                    "is what deadlocked and is why {2} "
                    "exists".format(", ".join(DEADLOCKED_PR), context, ISSUE),
                )

    def test_no_required_context_workflow_filters_its_pull_request_trigger(self):
        # The structural form of the invariant, and the reason the fix is a
        # deletion rather than a longer list: any path filter has a PR class it
        # excludes — an allowlist excludes whatever it forgot, `paths-ignore`
        # excludes any PR confined to the ignored set — and no filter can see
        # the ruleset it must stay in sync with. Runner minutes are the price;
        # a check that never reports costs a deadlocked PR.
        for context, (workflow, _) in REQUIRED_CONTEXTS.items():
            with self.subTest(context=context):
                filters = trigger_filters(workflow, "pull_request")
                self.assertEqual(
                    {}, filters,
                    "{0} produces the required context {1!r}, so its "
                    "`pull_request:` trigger must carry no `paths`/"
                    "`paths-ignore` filter at all — skip the expensive work "
                    "inside the job if it is wasteful, but let the check "
                    "report ({2})".format(workflow, context, ISSUE),
                )


class PostMergeCoverageTests(unittest.TestCase):
    """`main`'s post-merge run is the backstop for what a PR run raced past."""

    def test_ci_tests_push_trigger_covers_every_commit_that_lands_on_main(self):
        filters = trigger_filters("ci-tests.yml", "push")
        self.assertEqual(
            {}, filters,
            "ci-tests.yml's `push:` filter must match its `pull_request:` one, "
            "so the suites that gate a PR also run on the commit that lands — "
            "otherwise a release commit (or any commit outside the filter) "
            "merges to main with no post-merge run at all ({0})".format(ISSUE),
        )


if __name__ == "__main__":
    unittest.main()
