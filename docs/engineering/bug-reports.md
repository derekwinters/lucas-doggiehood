# Bug reports & diagnostics

*Covers: the on-device **bug report** snapshot ([#692](https://github.com/derekwinters/lucas-doggiehood/issues/692)) — what it contains, the two rules it is built to, where the file lands, and why none of it leaves the device on its own — plus how it **gets off the device** when the player asks it to ([#695](https://github.com/derekwinters/lucas-doggiehood/issues/695)). The three buttons that produce it are specified in [Settings menu](../specs/ui/settings.md#debug-sub-tabs-716); this page is the payload and the plumbing.*

When something goes wrong on the tablet there is otherwise no way to get the game's state off the device, so a nuanced bug ("the truck did a weird thing after I unlocked that tile") has to be described from memory. The **Settings → Debug → Reports** sub-tab snapshots everything the game knows about itself into one plain-text document, and then copies it to the clipboard (**Copy bug report**), writes it to a file (**Save bug report**), or hands that file to the Android share sheet (**Share bug report**).

The value is the payload, not the button: the snapshot has to be complete enough to *reproduce* the bug.

## Where the work lives

| Piece | Assembly | What it does |
|---|---|---|
| `Doggiehood.Core.Diagnostics.DiagnosticReport` | Core (engine-free) | Renders the whole document. Every formatting and content decision lives here, so the payload is unit-tested against a hand-built `GameState` with no Unity install. |
| `DiagnosticEnvironment` / `DiagnosticLogEntry` / `DiagnosticNumbers` | Core | The device facts and log lines as plain data, plus the named sizes (`LogTailSize`, `ReportSchemaVersion`). Core describes the device without making a single engine call. |
| `Doggiehood.Unity.BugReportBuilder` | Unity | Gathers what only the engine and the live scene know — build/device/screen/uptime, the buffered log tail, where each dog is standing — and hands it to Core. No decision logic. |
| `Doggiehood.Unity.DiagnosticLogBuffer` | Unity | A bounded ring buffer over `Application.logMessageReceived`, installed by `WorldBootstrap` **first**, before any other startup work. |
| `Doggiehood.Unity.BugReportFile` / `BugReportCopy` | Unity | The disk boundary and the toast copy. |
| `Doggiehood.Core.Diagnostics.BugReportSummary` | Core | The one-line summary a shared report travels with — build, device, timestamp — capped and flattened so it can never become a body of text. |
| `Doggiehood.Core.Diagnostics.FileProviderAuthority` | Core | The `content://` authority rule, derived from the application id. Read by both the manifest and the runtime, so the two cannot disagree. |
| `Doggiehood.Unity.IBugReportShareTarget` | Unity | The seam between "the file is written" and "the OS is offering it". |
| `Doggiehood.Unity.AndroidShareTarget` | Unity | The JNI half — deliberately zero-logic, because it is the one piece no test can reach. |

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

`Application.persistentDataPath/bug-reports/bugreport-<yyyyMMdd-HHmmss>.txt` — beside the local save file, in the app's own storage. The timestamp runs to the second, so two reports taken in one session never collide and the newest is obvious in a listing. On Android that is the app-private external files directory. Pulling it off the device by hand means a file manager or `adb`; [#695](https://github.com/derekwinters/lucas-doggiehood/issues/695)'s share sheet removes that friction, and shares this exact file rather than writing a second one.

Nothing prunes the folder: reports are small and rare, and silently deleting the evidence a player just captured would be the wrong default.

## Sharing a report off the device ([#695](https://github.com/derekwinters/lucas-doggiehood/issues/695))

**Share bug report** writes the same timestamped file **Save bug report** produces — there is one file-writing path, not two — and then hands *that file* to Android's standard share sheet. The player picks the destination app. This is the way a report is expected to travel; clipboard and file stay as the fallbacks.

**Invariant — a shared bug report is never silently truncated.** The report travels as a **file attachment** (`EXTRA_STREAM`), never as message body text. `EXTRA_TEXT` (and the subject) carry only a short summary line — app version, device, timestamp — so that if a receiving app drops the attachment entirely, the recipient can still see which build it came from and ask for the file. The obvious implementation puts the report in `EXTRA_TEXT` and is one line of code, but a report is tens of kilobytes and a receiving app is free to trim a long text extra (SMS certainly will). A report that arrives with its `LOG` section quietly cut off is worse than one that never sent, because nobody can tell it happened.

### The seam, and why it sits where it does

JNI cannot run in EditMode, so the Unity layer splits and only the thin end is untestable:

| Above the seam — tested | Below the seam — device only |
|---|---|
| Rendering the snapshot, writing the file, composing the summary (`BugReportSummary`), deriving the authority (`FileProviderAuthority`), wiring the row, choosing the platform's target, raising the toast | Building the `ACTION_SEND` intent, `FileProvider.getUriForFile`, `createChooser`, `startActivity` |

`IBugReportShareTarget` has a single method, `Share(filePath, summary)`. EditMode asserts the whole path above it against a fake target that records what it was handed — that the file written is the file shared, that the summary names the build/device/time, and that it is emphatically *not* the report. `AndroidShareTarget` below it has no branches and no formatting: everything that could be decided differently was decided above.

Off Android there is no share sheet, so `BugReportShareTargets.ForThisPlatform()` returns no target and the row falls back to **Save bug report**'s behaviour and toast. That choice is made on `Application.platform` at runtime, not on a `UNITY_ANDROID` compile symbol — with the Android build target selected that symbol is defined *in the Editor too*, which would hand the Editor a share sheet that does not exist.

### The FileProvider plug-in

Handing out a `file://` URI throws `FileUriExposedException` on modern Android, so the file is exposed through a `FileProvider`. That needs a manifest declaration and an XML resource, both of which live in a hand-authored Android **Library Project** plug-in:

```
Assets/Plugins/Android/doggiehood-share.androidlib/
  build.gradle                       # com.android.library + namespace + androidx.core
  src/main/AndroidManifest.xml       # the <provider> declaration
  src/main/res/xml/file_paths.xml    # what the provider may serve
```

Two deliberate choices there, both load-bearing:

- **A library plug-in, not a custom main manifest.** `Assets/Plugins/Android/AndroidManifest.xml` *replaces* Unity's own generated manifest wholesale — get one element wrong and the game stops launching, on device, where the suite cannot see it. A library manifest is **merged** by Gradle, so it can only add. It is also the only option that works at all: Unity does not support Android resources outside a library project, and `file_paths.xml` is a resource.
- **The authority is derived, never typed.** The manifest uses Gradle's `${applicationId}` placeholder and the runtime uses `FileProviderAuthority.For(Application.identifier)` — the same rule, one build step apart. Two apps cannot share an authority string, so hard-coding `com.derekwinters.doggiehood.fileprovider` would break the side-by-side `.debug` build ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)/[#734](https://github.com/derekwinters/lucas-doggiehood/issues/734)) — exactly the build Lucas is most likely to be running when he hits a bug.

`file_paths.xml` grants the `bug-reports/` folder and nothing else — never the storage root, so the share sheet can read the one report the player just captured, not the save file beside it. It names **both** the external and internal files roots because `Application.persistentDataPath` is the app's external files dir by default and its internal one when the Write Permission player setting is Internal; only one is ever live, and which is a player setting rather than something this code picks.

Per [Hand-authoring Unity serialized assets](unity-serialization.md), every element, attribute and `.meta` above is pinned by text assertions in `Assets/Tests/EditMode/BugReportShareProviderTests.cs`, including the negative ones: the library manifest declares no `<activity>`, never claims the launcher, carries no `package=` attribute (AGP 8+ takes the namespace from `build.gradle`), and no custom main manifest exists to override Unity's.

### What only a device can confirm

The suite proves the file is written, the right path and summary cross the seam, the toast fires, the fallback holds, and the manifest says exactly what it should. CI additionally builds a real debug APK, so a broken Gradle module or malformed manifest fails there. **None of that proves the intent resolves.** That the share sheet actually opens, that the chooser lists apps, and that the receiving app can read the `content://` URI are only observable on real hardware — a wrong authority or an unserved path fails silently at that last step. That check is a physical one and is tracked separately for Derek.

## Privacy

The game has **no accounts, no network calls and no location data** ([product scope](../specs/product-scope.md)), so there is nothing personal in the snapshot to leak. The only device-identifying values are the **device model** and **OS version**, which are exactly what makes an Android-specific bug diagnosable in the first place.

A report goes only where the player sends it: the clipboard, a file in the app's own storage, or an app they pick themselves from the share sheet. Nothing here opens a socket — the clipboard and file destinations were chosen in #692 precisely because they need no credentials, no network and no manifest change, and #695's share hands a file to the OS rather than sending anything. It is *player-initiated*: it is not an automatic upload, and no issue should make it one ([product scope](../specs/product-scope.md#connectivity)).

## Adding to the report

Add a section, or a line to one, in `DiagnosticReport` — with a Core test for it. Two rules follow from the invariants above:

- Emit the header (and any named sub-list) unconditionally; print `(none)` when there is nothing to say.
- Read, never write. Anything that would advance a clock, roll a die, or lazily initialize state does not belong in a report — read the persisted fact instead, or report that it is unavailable.

A debug affordance whose job is to expose *more of the game's state* is a section here, not a new Debug row: the snapshot is the one place that grows. A new **way for that snapshot to travel** is a different thing and does get a row — that is what [#695](https://github.com/derekwinters/lucas-doggiehood/issues/695)'s share sheet is — but **Reports** is full at three rows now, so the next one needs a new sub-tab and therefore a wireframe decision.
