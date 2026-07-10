# Camera, Navigation & Controls

*Epic: [#14](https://github.com/derekwinters/lucas-doggiehood/issues/14)*

## No player character

There is no visible player-controlled character in the world. The player is an unseen observer viewing the neighborhood from above and interacting with it directly — tapping dogs, houses, and items rather than moving an avatar. ([#19](https://github.com/derekwinters/lucas-doggiehood/issues/19))

## Navigation

Drag/swipe to pan the camera across the neighborhood; pinch to zoom in and out — the same interaction model as panning around a map. Tapping a dog or house triggers its interaction directly. ([#20](https://github.com/derekwinters/lucas-doggiehood/issues/20))

## Camera angle

Isometric / angled top-down camera — in the spirit of SimCity or Animal Crossing — rather than a straight bird's-eye view or a free-rotating 3D orbit camera. This shows house facades and roofs and keeps dogs easy to spot and tap. ([#21](https://github.com/derekwinters/lucas-doggiehood/issues/21))

## Orientation

The app runs in **landscape** orientation, to better show off the neighborhood scene at once. ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22))

## Build checklist

- [ ] Fixed isometric camera angle (no free rotation/orbit)
- [ ] Pan via drag/swipe within the bounds of the current neighborhood scene
- [ ] Pinch-to-zoom with sane min/max zoom limits
- [ ] Tap-to-interact hit-testing on dogs and houses works at all zoom levels
- [ ] App is locked to landscape orientation
- [ ] No player avatar/character exists anywhere in the scene
