# dejavu — bundled UI font (provenance)

The game's runtime-built UGUI (the Settings panel, #219) needs a **bundled**
font: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` is Editor-only
and gets stripped from the Android build, so the panel's text drew nothing on
device (#291).

**DejaVu Sans** is bundled to fix this:

- Shipped copy (imported, `includeFontData: 1`, loaded via `Resources.Load`):
  `Assets/Art/UI/Fonts/Resources/DejaVuSans.ttf`
- Source: <https://dejavu-fonts.github.io/> (obtained from the Debian
  `fonts-dejavu-core` package, file unmodified).
- License: Bitstream Vera + public-domain DejaVu changes — redistributable and
  bundle-able in a larger package. Full text: [`LICENSE.txt`](LICENSE.txt).

Chosen over the Kenney display font already staged in `art-source/` because
DejaVu Sans has the broad glyph coverage the panel needs — including the ✕
(U+2715) close affordance — and reads well at small body sizes. (DejaVu does
**not** cover the fullwidth plus U+FF0B, so the #286 "Add coins" action uses a
plain ASCII `+`; see `docs/specs/ui/settings.md`.)
