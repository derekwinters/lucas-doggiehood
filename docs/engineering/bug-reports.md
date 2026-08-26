# Bug reports & diagnostics

*Covers: the on-device **bug report** snapshot ([#692](https://github.com/derekwinters/lucas-doggiehood/issues/692)) — what it contains, the two rules it is built to, where the file lands, and why none of it leaves the device on its own. The two buttons that produce it are specified in [Settings menu](../specs/ui/settings.md#debug-sub-tabs-716); this page is the payload.*

When something goes wrong on the tablet there is otherwise no way to get the game's state off the device, so a nuanced bug ("the truck did a weird thing after I unlocked that tile") has to be described from memory. The **Settings → Debug → Reports** sub-tab snapshots everything the game knows about itself into one plain-text document, and then either copies it to the clipboard (**Copy bug report**) or writes it to a file (**Save bug report**).

The value is the payload, not the button: the snapshot has to be complete enough to *reproduce* the bug.

## Where the work lives

| Piece | Assembly | What it does |
|---|---|---|
| `Doggiehood.Core.Diagnostics.DiagnosticReport` | Core (engine-free) | Renders the whole document. Every formatting and content decision lives here, so the payload is unit-tested against a hand-built `GameState` with no Unity install. |
| `DiagnosticEnvironment` / `DiagnosticLogEntry` / `DiagnosticNumbers` | Core | The device facts and log lines as plain data, plus the named sizes (`LogTailSize`, `ReportSchemaVersion`). Core describes the device without making a single engine call. |
| `Doggiehood.Unity.BugReportBuilder` | Unity | Gathers what only the engine and the live scene know — build/device/screen/uptime, the buffered log tail, where each dog is standing — and hands it to Core. No decision logic. |
| `Doggiehood.Unity.DiagnosticLogBuffer` | Unity | A bounded ring buffer over `Application.logMessageReceived`, installed by `WorldBootstrap` **first**, before any other startup work. |
| `Doggiehood.Unity.BugReportFile` / `BugReportCopy` | Unity | The disk boundary and the toast copy. |

## What a report contains

Each section opens with a stable `== SECTION ==` header, so a report is greppable and two reports diff cleanly.

| Section | Contents |
|---|---|
| `REPORT` | Report schema version, timestamp, app version, build flavor + application id, platform, device model, OS version, screen size + DPI, session uptime |
| `SAVE` | The verbatim `SaveCodec.Save(state)` blob — the reproducibility payload |
| `TUNING` | Every `TuningCatalog` field: name, label, current value, shipping default, and a `*` marker when the live value differs from the default |
| `DEBUG` | Every registered `DebugToggleRegistry` toggle and its state |
| `ECONOMY` | Coin balance, next tile-unlock cost, next house-build cost, per-level upgrade costs |
| `MAP` | Map extent, placed/road tile counts, unlocked tiles **in unlock order** with their tile types, auto-placed green spaces, the current frontier |
| `HOUSES` | Per house: id, lot coordinate, quadrant, level, vacancy, variant, upgrade eligibility, occupants — plus assigned-but-unbuilt lot variants |
| `DOGS` | Per dog: name, breed, coat, personality, house, current world position, location/state, happiness, active quest |
| `QUESTS` | Active quests (type, dog, item, cost, target house, status, delivery phase), the rotation/refresh clock, the pacing window and accumulator |
| `ONBOARDING` | `OnboardingComplete`, whether the tutorial sequence would run, reward-chain step, `OnboardingUpgradeTargetHouseId` |
| `ITEMS` | Placed items and decorations |
| `LOG` | The last `DiagnosticLogTailSize` (200) log lines, newest last, with severity; exceptions keep their stack trace |

The single highest-value line is the `SAVE` blob: paste it back into a dev build and the neighborhood is exactly as it was. Everything else is the context a save file does not carry — which sliders had been dragged off their shipping defaults, which debug toggles were on, where each dog was standing at that instant, and what the log said just before things went sideways.

`LOG` is **last** on purpose: it makes the end of the report identifiable, so a truncated report is recognizable as truncated. [#695](https://github.com/derekwinters/lucas-doggiehood/issues/695) (the Android share sheet) leans on that.

**Invariant — a diagnostic report never silently omits a system.** Every section header, and every named sub-list inside a section, is emitted on **every** report, an empty one printing `(none)` — so a missing system reads as "there were none" rather than as "this build forgot to capture it". A snapshot that quietly drops a system is worse than no snapshot, because it is trusted. A Core reflection test also fails the suite if a `TuningConfig` field is missing from `TUNING`, so the report cannot drift as new tunables land.

**Invariant — a diagnostic report is read-only.** Generating one never mutates `GameState`, `TuningConfig`, the save file, or any toggle: rendering twice from the same state produces byte-identical output, and nothing about the world moves. Snapshotting a bug must not change the bug.

## Where a saved report lands

`Application.persistentDataPath/bug-reports/bugreport-<yyyyMMdd-HHmmss>.txt` — beside the local save file, in the app's own storage. The timestamp runs to the second, so two reports taken in one session never collide and the newest is obvious in a listing. On Android that is the app-private external files directory; pulling it off the device today means a file manager or `adb`, which is exactly the friction [#695](https://github.com/derekwinters/lucas-doggiehood/issues/695) removes with the share sheet.

Nothing prunes the folder: reports are small and rare, and silently deleting the evidence a player just captured would be the wrong default.

## Privacy

The game has **no accounts, no network calls and no location data** ([product scope](../specs/product-scope.md)), so there is nothing personal in the snapshot to leak. The only device-identifying values are the **device model** and **OS version**, which are exactly what makes an Android-specific bug diagnosable in the first place.

A report goes only where the player sends it: the clipboard, or a file in the app's own storage. Nothing here opens a socket — the clipboard and file destinations were chosen in #692 precisely because they need no credentials, no network and no manifest change. #695 adds a *player-initiated* share; it does not add an automatic upload, and no issue should.

## Adding to the report

Add a section, or a line to one, in `DiagnosticReport` — with a Core test for it. Two rules follow from the invariants above:

- Emit the header (and any named sub-list) unconditionally; print `(none)` when there is nothing to say.
- Read, never write. Anything that would advance a clock, roll a die, or lazily initialize state does not belong in a report — read the persisted fact instead, or report that it is unavailable.

A debug affordance whose job is to get state *off the device* is a section here, not a new Debug row: the snapshot is the one place that grows.
