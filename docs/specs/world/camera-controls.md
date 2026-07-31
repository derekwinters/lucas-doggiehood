# Camera, Navigation & Controls

*Epic: [#14](https://github.com/derekwinters/lucas-doggiehood/issues/14)*

## No player character

There is no visible player-controlled character in the world. The player is an unseen observer viewing the neighborhood from above and interacting with it directly — tapping dogs, houses, and items rather than moving an avatar. ([#19](https://github.com/derekwinters/lucas-doggiehood/issues/19))

## Navigation

Drag/swipe to pan the camera across the neighborhood; pinch to zoom in and out — the same interaction model as panning around a map. Tapping a dog or house triggers its interaction directly. ([#20](https://github.com/derekwinters/lucas-doggiehood/issues/20))

**A tap over UI never reaches the world behind it.** World tap-routing is modal-aware: when an open dialog/menu sits under the pointer, the tap is absorbed by that UI and the world raycast is skipped — so tapping **Accept** on a quest never also opens a house behind the button, and tapping **Close** never re-triggers the thing it sits over ([#422](https://github.com/derekwinters/lucas-doggiehood/issues/422)). The router bails before any world hit-test when the tap is over a UGUI graphic (`EventSystem.IsPointerOverGameObject`, mouse and touch-`fingerId` overloads — every modal overlay carries a full-screen raycast-blocking scrim, so this reads true anywhere over it) or over the still-IMGUI HUD Settings gear (a screen-space rect check against `HudOverlay.ComputeGearRect`, interim until the gear migrates to UGUI in [#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)).

**The pan bounds grow with the map.** Panning is clamped to the neighborhood's extent, but that extent is not fixed: it is derived from the live tile map (`Doggiehood.Core.World.MapExtent.Covering` — a named margin beyond the outermost tiles) and **recomputes when a zone is unlocked** (`CameraController.RecomputeBoundsFromMap`), so the player can always pan over to reach a newly unlocked zone rather than being clamped out of it ([#373](https://github.com/derekwinters/lucas-doggiehood/issues/373)). The starting bounds derive from the same map-based path (the seeded origin tile), so there is one derivation for both the initial world and every expansion.

## Camera angle

Isometric / angled top-down camera — in the spirit of SimCity or Animal Crossing — rather than a straight bird's-eye view or a full free-orbit 3D camera. This shows house facades and roofs and keeps dogs easy to spot and tap. ([#21](https://github.com/derekwinters/lucas-doggiehood/issues/21))

The **pitch (45°) and the orthographic projection are fixed**. The **yaw rotates freely**, driven by a two-finger twist gesture: the neighborhood follows your fingers — twisting clockwise turns the scene clockwise (the camera itself yaws the opposite way), the same "content follows the finger" convention as drag-to-pan. Rotation is continuous — it does not snap to fixed angles and is not clamped to a range. This reopens the original "no free rotation/orbit" decision of #21 for yaw only, while keeping the recognisable angled-down look. ([#203](https://github.com/derekwinters/lucas-doggiehood/issues/203))

> Note: the rotation control (#203) initially left the fixed-angle scene-visibility assumption in place — that content only ever needed to read well at the single fixed yaw. **Camera-facing world markers now track the live camera yaw** so they stay head-on at every rotation: speech bubbles and the map-expansion lock icon share one Core seam (`CameraFacing.Resolve(cameraYaw)` — fixed orthographic pitch, live yaw, zero roll) that the Unity layer re-applies each frame ([#266](https://github.com/derekwinters/lucas-doggiehood/issues/266)). Broader scene *content* that is only authored for the fixed angle (e.g. facade-only art) is a separate follow-on, not covered by the marker-facing work.

## Orientation

The app is **locked to landscape** orientation — it never rotates to portrait — to better show off the neighborhood scene at once. ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22))

This is one facet of the project's platform target, stated authoritatively once — tablet, landscape, and the 1920×1200 (16:10) UI reference resolution — under [UI Wireframes → Target platform & reference resolution](../ui/index.md#target-platform-reference-resolution). It is enforced in Unity by allowing only the two landscape orientations (portrait disabled, so the tablet auto-rotates between landscape-left and landscape-right but never to portrait) and scaling the UI canvas from that reference with a `CanvasScaler` ([#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)).

## Build checklist

- [ ] Fixed pitch (45°) and orthographic projection, with free twist-driven yaw rotation (continuous, no snapping/clamping)
- [ ] Camera-facing world markers (speech bubbles, map-expansion lock icon) track the live camera yaw and read head-on at every rotation (#266); broader fixed-angle scene-art readability remains a follow-on
- [ ] Pan via drag/swipe within the bounds of the current neighborhood scene (the bounds grow with the map, recomputed from the live tile extent on zone unlock — [#373](https://github.com/derekwinters/lucas-doggiehood/issues/373))
- [ ] Pinch-to-zoom with sane min/max zoom limits
- [ ] Tap-to-interact hit-testing on dogs and houses works at all zoom levels
- [ ] A tap over an open dialog/menu (or its scrim, or the HUD gear) is absorbed by that UI and never reaches a world interactable behind it ([#422](https://github.com/derekwinters/lucas-doggiehood/issues/422))
- [ ] App is locked to landscape orientation
- [ ] No player avatar/character exists anywhere in the scene
