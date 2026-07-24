# UI Wireframes

*Epic: [#171](https://github.com/derekwinters/lucas-doggiehood/issues/171). Process: [UI Design Process](../../engineering/ui-design-process.md).*

This section is the **layout contract** for the game's UI. Where [Art & UI Style](../world/art-style.md) settles visual *style* (the "Candy Cottage" direction), the pages here settle *layout* — the regions each screen has, how they're anchored, and their sizes/margins as named constants.

Every page here is a distilled, **approved** wireframe. Nothing lands in this section until it has gone through the [UI Design Process](../../engineering/ui-design-process.md) loop (propose → review → approve → distill). A development agent implementing a UI screen treats the matching page here as authoritative for layout; if a screen has no page here yet, that's the [gate](../../engineering/ui-design-process.md#the-gate) — stop and flag rather than inventing a layout.

## Target platform & reference resolution

Doggiehood is primarily a **tablet** game, **landscape-locked** ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22)). Every UI wireframe's layout constants are authored against a **1920×1200 (16:10)** reference resolution — the pixel values on each page and in each mockup are the component's true size at that reference. On device, a Unity `CanvasScaler` scales from this reference so the same composition holds across the supported range of tablet sizes and aspect ratios.

*Reference-resolution rollout and any per-screen scaling follow-ups are tracked in [#256](https://github.com/derekwinters/lucas-doggiehood/issues/256).*

## Structure

- **`docs/specs/ui/<screen>.md`** — one page per distinct screen, panel, or overlay: the structured text spec.
- **`docs/specs/ui/mockups/<screen>.html`** — the visual HTML mockup for that screen, corresponding 1:1 with its text spec.
- **[Shared UI Components](shared-components.md)** — atomic reusable pieces (pill button, currency chip, chip shape) documented once and referenced by every screen, rather than re-specified per screen.

## Wireframes

- [Shared UI Components](shared-components.md) — the shared reference every per-screen wireframe points to.
- [HUD](hud.md) — the persistent HUD; currency chip placement ([#174](https://github.com/derekwinters/lucas-doggiehood/issues/174)).

*Per-screen wireframes are added here as they're approved (tracked under epic [#171](https://github.com/derekwinters/lucas-doggiehood/issues/171): [#173](https://github.com/derekwinters/lucas-doggiehood/issues/173) shared components, [#174](https://github.com/derekwinters/lucas-doggiehood/issues/174) HUD currency chip, [#175](https://github.com/derekwinters/lucas-doggiehood/issues/175) conversation panel, [#176](https://github.com/derekwinters/lucas-doggiehood/issues/176) onboarding overlay, [#177](https://github.com/derekwinters/lucas-doggiehood/issues/177) dog profile view).*

## Per-screen page template

Every new per-screen wireframe page follows this shape. Copy it, fill each section, and commit the matching `mockups/<screen>.html` alongside.

```markdown
# <Screen name>

*Wireframe issue: #NN. Implements/covers: `<UnityTypeName>`. Approved: <link to approval comment>.*
*Mockup: [mockups/<screen>.html](mockups/<screen>.html).*

## Purpose

One or two sentences: what this screen/panel/overlay is for and when it appears.

## Regions

The composition, top-level region by region. For each region, name it, say what it
contains, and reference any [shared component](shared-components.md) it reuses rather
than re-specifying it.

| Region | Contains | Shared component |
|---|---|---|
| ... | ... | ... |

## Anchors & layout constants

Every size, margin, and anchor as a **named constant** — never a fixed pixel position,
so the layout holds across Android aspect ratios. These are the exact constants the
implementation declares (per [#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables));
EditMode tests assert the built UI against them.

| Constant | Value | Applies to |
|---|---|---|
| `ExampleMarginPx` | ... | ... |

## Notes

Anything the mockup shows that the tables don't capture — behavior on the smallest
supported aspect ratio, empty/overflow states, etc. Style itself lives in
[Art & UI Style](../world/art-style.md); this page is layout only.
```
