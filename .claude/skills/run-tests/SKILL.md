---
name: run-tests
description: >
  Run Doggiehood's test suites headlessly and summarize pass/fail results.
  Use whenever tests need to run: during red-green-refactor loops, before
  committing, or to check the health of the tree.
---

# Run Doggiehood tests

Two suites exist (docs/engineering/testing.md). Run what the environment
supports and report results honestly — never claim a suite passed that
didn't run.

## 1. Core NUnit tests (always runnable — no Unity needed)

```bash
dotnet test CoreTests/Doggiehood.Core.Tests --logger "console;verbosity=normal"
```

- Requires only a .NET SDK (8.x). If `dotnet` is missing, install it first
  (e.g. `apt-get install -y dotnet-sdk-8.0`).
- The csproj compiles Core sources directly from `Assets/Scripts/Core/`,
  so this tests exactly what Unity ships.
- Report the final `Passed!/Failed!` line, and for failures list each
  failing test name with its assertion message.

## 2. Unity EditMode tests (needs a Unity editor install)

Only attempt locally if the pinned editor exists (version in
`ProjectSettings/ProjectVersion.txt`). Typical Hub path shown here:

```bash
UNITY="$HOME/Unity/Hub/Editor/$(awk '{print $2}' ProjectSettings/ProjectVersion.txt)/Editor/Unity"
"$UNITY" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults "$(pwd)/editmode-results.xml" -logFile -
```

- Exit code 0 = all passed; parse `editmode-results.xml` (NUnit XML:
  `passed`/`failed` counts on the root `test-run` element) for the summary.
- If no Unity editor is installed (the normal case for remote dev
  environments), say so explicitly and rely on CI: `ci-tests.yml` runs
  EditMode headlessly via game-ci on every PR touching app files.

## Summary format

End with a short table: suite | ran? | passed | failed, plus the failing
test names if any. A suite that couldn't run is "not run (reason)" — never
a pass.
