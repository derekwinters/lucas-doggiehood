# art-source — raw asset packs (staging, not imported)

Raw third-party asset packs live here **outside `Assets/`** on purpose: Unity only
imports files under `Assets/`, so staging them here keeps ~1,700 raw files (PNG, SVG,
vector sources, sample HTML, fonts) from being imported and generating `.meta`/GUID
churn before we've decided what we actually use.

Nothing here is referenced by the game yet. The build does not depend on this folder.

## Contents

### `kenney/ui-pack/` — Kenney "UI Pack" (CC0)
Rounded 9-slice panels, pill buttons, checkboxes/toggles, sliders, cursors — plus a
rounded TTF font and UI `.ogg` sounds. Candidate chrome for the Candy Cottage UI
(`docs/specs/world/art-style.md`), tinted to our bright palette rather than used as-is.

### `kenney/game-icons/` — Kenney "Game Icons" (CC0)
~225 flat white icons (gear, wrench/tools, close, checkmarks, arrows, …), tintable to
the ink color. Candidate icons for the settings/debug UI.

**License:** both packs are CC0 (public domain) — see each pack's `License.txt` /
`license.txt`. No attribution required.

## Cleanup later (TODO)

This is a raw dump kept so the assets aren't lost — it is **not** the final home.
When the settings/UI work is built (#218 wireframe → #219 implementation):

1. Select only the specific sprites we use; import **those** into `Assets/Art/UI/`
   with correct sprite import settings, following `docs/engineering/unity-serialization.md`
   (pin GUIDs + sprite internal IDs) and guarded by a serialization-level EditMode test.
2. Prune the unused remainder of these packs from the repo.
3. Decide whether the Kenney rounded font / UI sounds are adopted or dropped.

Refs #218, #219. UI chrome direction: `docs/specs/world/art-style.md` ("Candy Cottage").
