# UI Design Process: Wireframe Before Code

*Epic: [#171](https://github.com/derekwinters/lucas-doggiehood/issues/171) — Issue: [#172](https://github.com/derekwinters/lucas-doggiehood/issues/172). Refs [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161), [#65](https://github.com/derekwinters/lucas-doggiehood/issues/65), [#156](https://github.com/derekwinters/lucas-doggiehood/issues/156).*

**Every UI screen, panel, or overlay requires an approved wireframe before any implementation code is written — including graybox.** [Art & UI Style](../specs/world/art-style.md) settles *visual style* (the "Candy Cottage" direction — outlines, shadows, pill shapes, colors); it never settled *layout* — what regions a screen has, how they're anchored, and how they're sized. That gap is what [#156](https://github.com/derekwinters/lucas-doggiehood/issues/156) surfaced: a dialogue panel was built, then found structurally inadequate ("doesn't have anything besides Accept") only once Derek opened the editor. This process closes it, the same way [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161) closed the inline-literals gap — by making the design exist, and be approvable, before code.

This doc is the same tier as [Testing Strategy](testing.md): a non-negotiable part of how work gets done here.

## What a wireframe is: a dual artifact

A wireframe is **two files that correspond 1:1**, both living under [`docs/specs/ui/`](../specs/ui/index.md):

1. **A structured text spec** — the per-screen page (e.g. `docs/specs/ui/conversation-panel.md`). It enumerates the screen's regions, their anchors, and their sizes/margins **as named constants** — never fixed pixel positions, so the layout holds across the range of Android aspect ratios. This page follows the [per-screen page template](../specs/ui/index.md#per-screen-page-template).
2. **A visual mockup (HTML)** — committed to `docs/specs/ui/mockups/` (e.g. `docs/specs/ui/mockups/conversation-panel.html`). A self-contained static HTML file that renders the same regions the text spec describes, so Derek and Lucas can *see* the composition. It corresponds 1:1 with the text spec: every region and constant in one appears in the other.

Reusable atomic pieces (pill button, currency chip, chip shape) are **not** re-specified per screen. They're documented once in [Shared UI Components](../specs/ui/shared-components.md) and referenced by every screen that uses them.

## The named constants are the origin, not an afterthought

The named size/margin/anchor constants in the text spec **are** the constants implementation code declares under [#161's no-inline-literals rule](tech-stack.md#geometry-layout-and-tuning-values-are-named-variables). The wireframe is where those values are *born and approved* — they are not invented ad hoc while writing the MonoBehaviour. When implementation begins, the code's named layout constants trace directly back to the approved spec, and EditMode tests assert the built UI's actual anchors/margins/sizes against them.

This is what lets Derek and Lucas **approve a design without loading the game**: structural correctness is test-verified against the spec's constants, so only final art/color/font polish still wants a glance at a real build.

## The loop

```
propose ──▶ review ──▶ approve ──▶ distill ──▶ implement ──▶ verify
(dual      (Derek/    (recorded   (into        (against     (EditMode
 artifact)  Lucas)     on issue)   docs/specs/  named        tests vs.
                                   ui/)         constants)   constants)
```

1. **Propose.** On the wireframe's GitHub issue, produce the dual artifact: the structured text-spec page and the corresponding HTML mockup. Nothing about the screen's *style* gets re-decided here — that's [Art & UI Style](../specs/world/art-style.md)'s job; the proposal composes already-decided style into a layout.
2. **Review.** Derek and Lucas review the proposal — reading the text spec and opening the HTML mockup. Feedback loops back into the proposal until it's right.
3. **Approve.** Approval is recorded on the wireframe issue (a comment or a checked box), the same lightweight record used elsewhere. Approval is of the layout, its regions, and its named constants.
4. **Distill.** The approved wireframe lands in [`docs/specs/ui/`](../specs/ui/index.md) as the screen's page plus its mockup — this is now the authoritative layout contract for that screen, just like any other specs page.
5. **Implement.** Implementation code declares the spec's named constants (per [#161](https://github.com/derekwinters/lucas-doggiehood/issues/161)) and builds the layout against them — test-first, per [Testing Strategy](testing.md).
6. **Verify.** EditMode tests assert the built UI's actual anchors, margins, and sizes match the named constants. CI is the authoritative green ([EditMode tests run in CI](testing.md#known-limitation-editmode-tests-run-in-ci-not-in-agent-environments)).

## The gate

If an issue would touch a screen's **structure** (adding, removing, or repositioning regions of a panel/overlay/screen) and **no approved wireframe exists** for it, **stop and flag it** — do not implement, not even graybox. This has the same posture as the [docs-conflict rule](agent-workflow.md#how-an-issue-gets-worked): it's a design gap to resolve back in GitHub (file/finish the wireframe first), not something to invent mid-implementation.

Purely visual restyling that doesn't move structure (e.g. applying an already-approved shared-component style) is not gated by this rule — but if you're unsure whether a change is structural, treat it as structural and flag it.
