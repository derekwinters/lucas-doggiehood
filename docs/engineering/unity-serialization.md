# Hand-Authoring Unity Serialized Assets

Development agents work in sandboxes **without a Unity Editor**, so `.meta` files and `ProjectSettings.asset` edits are written by hand as YAML. Unity's serialization has several silent failure modes that cost real debugging rounds when the app icon and splash screen were wired up (PRs [#132](https://github.com/derekwinters/lucas-doggiehood/pull/132), [#133](https://github.com/derekwinters/lucas-doggiehood/pull/133)). This page is the accumulated field guide — read it before hand-authoring any Unity YAML.

## The golden rule: never guess — verify against real project files

Unity ignores serialized entries it doesn't understand **silently, at build time**. There is no error, no warning, no failing import — the feature just doesn't exist in the build. Guessed key names and enum values are therefore worse than compile errors: they ship.

Before writing a block you haven't written before, find real-world `ProjectSettings.asset` / `.meta` examples in public repos (web search for the key name) and copy the exact key names, value shapes, sibling keys, and ordering.

**Case study:** the Android adaptive icon was first serialized with a guessed `m_Kind: 5`. Unity ignored the entry and shipped the raw square legacy icon. Real files revealed the actual enum: `Legacy = 0`, `Round = 1`, `Adaptive = 2` — and that Unity expects all six density entries (432/324/216/162/108/81), not one.

## Known traps

### 1. GUID references require committed `.meta` files

The repo convention for **art models** (FBX/PNG under `Assets/Art/`) is to *not* commit `.meta` files for binaries — Unity generates them locally and nothing depends on the GUID.

That convention **inverts** the moment anything references the asset by GUID (`ProjectSettings.asset`, a scene, another asset): the `.meta` must be hand-authored with a **pinned GUID** and committed, or every machine generates a different GUID and the reference breaks. Current pinned-GUID assets: the app icon, its adaptive layers, and the splash cover art (all under `Assets/Art/Icon/` and `Assets/Art/Splash/`).

### 2. Sprite references use `fileID: 21300000` — and sprite IDs must be pinned

A texture asset exposes different sub-assets with different fileIDs:

| Reference | fileID |
|---|---|
| `Texture2D` (e.g. app icons) | `2800000` |
| `Sprite` sub-asset (e.g. **splash backgrounds**) | `21300000` |

`PlayerSettings.SplashScreen.background` is typed `Sprite`, so the splash background must reference `{fileID: 21300000, guid: ..., type: 3}` and the PNG's importer must produce a Sprite (`textureType: 8`, `spriteMode: 1`).

**The trap inside the trap:** modern Unity *randomizes* sprite internal IDs on fresh import. A hand-authored reference to `21300000` only survives a from-scratch Library rebuild (i.e. CI, or any fresh clone) if the `.meta` pins the ID via `internalIDToNameTable`:

```yaml
  internalIDToNameTable:
  - first:
      213: 21300000
    second: cover-art
```

See `Assets/Art/Splash/cover-art.png.meta` for the working example.

### 3. Some settings persist in editor-source AND runtime field pairs — set both

Unity sometimes stores the same value twice: an editor-facing "source" field and the field the **built player actually reads**, with the Editor UI copying source → runtime when you assign it. Hand-authoring only the source half produces a build where the feature silently falls back to its default.

**Case study:** the splash background. `splashScreenBackgroundSourceLandscape/Portrait` are the editor source slots; the runtime reads `m_SplashScreenBackgroundLandscape/Portrait`. Setting only the source pair passed every editor-level test and shipped a splash that showed just the solid background color. Both pairs must carry the same Sprite reference (and the guard test now pins all four keys).

If a real-world file shows the same reference under two key names, that's this pattern — author both.

### 4. EditMode tests must assert serialization, not `PlayerSettings` object references

`PlayerSettings` deserializes once at editor startup. On a fresh Library rebuild (every CI run), assets referenced by nothing in a scene are imported **lazily, after startup** — so `PlayerSettings.GetIconsForTargetGroup(...)` style APIs return null texture references for the whole session even when the serialized wiring is correct. A test asserting those references passes locally (warm Library) and fails on CI, or worse, can't ever pass on CI.

Instead:

- Assert on the **YAML text** of `ProjectSettings.asset` (regex the block, check pinned GUIDs and exact enum values — pin values like `m_Kind: 2` so a bad edit can't ship silently).
- Assert asset existence with `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)` followed by `LoadAssetAtPath` — never rely on something else having imported it first.
- For pinned sub-asset IDs, `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` verifies both GUID and fileID.

`Assets/Tests/EditMode/AppIconTests.cs` and `SplashScreenTests.cs` are the reference implementations.

### 5. Simulate the assertions before pushing

The sandbox can't run Unity, but every serialization-level assertion is plain text matching — simulate the exact regexes/contains-checks against the final files with a quick Python script before committing. This proves red→green locally-in-spirit and catches regex/formatting mistakes without burning a ~5-minute CI round trip. (YAML formatting details matter: e.g. Unity writes a trailing space after empty scalar keys like `m_SubKind: ` — copy formatting exactly.)

## Checklist for a new hand-authored block

- [ ] Found ≥1 real-world file containing the same block; copied key names, value shapes, sibling keys, ordering
- [ ] Every enum value confirmed from a real file (not guessed, not from memory)
- [ ] Any GUID-referenced asset has a committed `.meta` with pinned GUID (and pinned sprite internal ID if referenced as a Sprite)
- [ ] Serialization-level EditMode guard test written first, asserting exact values
- [ ] Assertions simulated against the final files before pushing
- [ ] Report states plainly what only CI / a device can confirm
