# Shared UI Components

*Wireframe issue: [#173](https://github.com/derekwinters/lucas-doggiehood/issues/173) (stub established in [#172](https://github.com/derekwinters/lucas-doggiehood/issues/172)). Style source: [Art & UI Style](../world/art-style.md) ([#65](https://github.com/derekwinters/lucas-doggiehood/issues/65)).*

Atomic, reusable UI pieces are documented **once, here**, and referenced by every per-screen [wireframe](index.md) that uses them — never re-specified per screen. This page is the shared reference: a screen's page names a component and points here, rather than restating its shape, outline, or shadow.

The **style** of every component below comes from the "Candy Cottage" direction settled in [Art & UI Style](../world/art-style.md). This page pulls the rules that apply to *every* component forward as the shared baseline; the full rationale and the reference mockup live on that page.

!!! note "Stub — full shared-component wireframe is [#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)"
    This page currently establishes the shared *style* baseline every component inherits. The complete shared-components wireframe — each component's regions, named layout constants, and its HTML mockup — is produced under [#173](https://github.com/derekwinters/lucas-doggiehood/issues/173) following the [UI Design Process](../../engineering/ui-design-process.md).

## Shared style baseline (Candy Cottage)

Every UI component inherits, from [Art & UI Style](../world/art-style.md):

- **Thick dark outlines** on all chrome.
- **Flat, hard drop-shadows** — no blur.
- **Chunky pill / rounded shapes** — pill-shaped buttons and chips.
- **Bold rounded sans-serif type.**

A sticker-book feel, chosen for legibility against the bright/saturated palette and low-poly toy-shelf look.

## Components

Each component gets its own subsection here once its wireframe is approved: its regions, named layout constants (per [#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)), and a reference into the shared [mockups/](index.md#structure). The initial set the style spec already calls out:

- **Pill button** — decoration/gift choice buttons, dialogue actions, menu buttons.
- **Currency chip** — the HUD currency indicator.
- **Speech-bubble indicator** — the "this dog has something to say" marker.
- **Dialogue box** — the panel chrome shared by conversation and onboarding surfaces.

Screens reuse these by name; they do not redefine them.
