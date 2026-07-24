# Onboarding

*Epic: [#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)*

The first time the game is opened, the first dog with a speech bubble walks the player through panning/zooming, tapping the speech bubble, and completing one simple quest — teaching the core loop naturally, without a separate tutorial screen. ([#44](https://github.com/derekwinters/lucas-doggiehood/issues/44))

The guidance is presented as a slim **bottom-center coach prompt** (not a top banner) that is layered over live gameplay, never blocks input, advances itself through the four steps as the player performs each real action, and auto-dismisses once the first quest is complete. Its layout — regions, anchors, and named size/margin constants — is the approved wireframe in [UI Wireframes → Onboarding overlay](ui/onboarding-overlay.md) ([#176](https://github.com/derekwinters/lucas-doggiehood/issues/176)). The current graybox top-banner rendering in `OnboardingOverlay` is superseded by that coach prompt and is removed by [#207](https://github.com/derekwinters/lucas-doggiehood/issues/207).

## Build checklist

- [ ] First-launch state is tracked (so onboarding only runs once)
- [ ] Guided prompts cover: panning, zooming, tapping a speech bubble, and completing a quest through to reward
- [ ] Onboarding uses the real game systems (an actual dog, actual quest, actual reward) rather than a separate scripted tutorial scene
- [ ] No blocking modal tutorial screen — guidance is layered over live gameplay
