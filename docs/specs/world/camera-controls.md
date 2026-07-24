# Camera, Navigation & Controls

*Epic: [#14](https://github.com/derekwinters/lucas-doggiehood/issues/14)*

## No player character

There is no visible player-controlled character in the world. The player is an unseen observer viewing the neighborhood from above and interacting with it directly — tapping dogs, houses, and items rather than moving an avatar. ([#19](https://github.com/derekwinters/lucas-doggiehood/issues/19))

## Navigation

Drag/swipe to pan the camera across the neighborhood; pinch to zoom in and out — the same interaction model as panning around a map. Tapping a dog or house triggers its interaction directly. ([#20](https://github.com/derekwinters/lucas-doggiehood/issues/20))

## Camera angle

Isometric / angled top-down camera — in the spirit of SimCity or Animal Crossing — rather than a straight bird's-eye view or a full free-orbit 3D camera. This shows house facades and roofs and keeps dogs easy to spot and tap. ([#21](https://github.com/derekwinters/lucas-doggiehood/issues/21))

The **pitch (45°) and the orthographic projection are fixed**. The **yaw rotates freely**, driven by a two-finger twist gesture: the neighborhood follows your fingers — twisting clockwise turns the scene clockwise (the camera itself yaws the opposite way), the same "content follows the finger" convention as drag-to-pan. Rotation is continuous — it does not snap to fixed angles and is not clamped to a range. This reopens the original "no free rotation/orbit" decision of #21 for yaw only, while keeping the recognisable angled-down look. ([#203](https://github.com/derekwinters/lucas-doggiehood/issues/203))

> Note: the fixed-angle scene-visibility assumption from [#181](https://github.com/derekwinters/lucas-doggiehood/issues/181) — that content only ever needs to read well at the single fixed yaw — is **deferred** and out of scope for the rotation control. Making all scene content (e.g. speech bubbles, facade-only art) read correctly at every yaw is tracked separately.

## Orientation

The app runs in **landscape** orientation, to better show off the neighborhood scene at once. ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22))

## Build checklist

- [ ] Fixed pitch (45°) and orthographic projection, with free twist-driven yaw rotation (continuous, no snapping/clamping); #181's fixed-angle visibility assumption deferred
- [ ] Pan via drag/swipe within the bounds of the current neighborhood scene
- [ ] Pinch-to-zoom with sane min/max zoom limits
- [ ] Tap-to-interact hit-testing on dogs and houses works at all zoom levels
- [ ] App is locked to landscape orientation
- [ ] No player avatar/character exists anywhere in the scene
