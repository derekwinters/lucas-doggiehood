# Onboarding

*Epic: [#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)*

The first time the game is opened, the first dog with a speech bubble walks the player through panning/zooming, tapping the speech bubble, and completing one simple quest — teaching the core loop naturally, without a separate tutorial screen. ([#44](https://github.com/derekwinters/lucas-doggiehood/issues/44))

## Build checklist

- [ ] First-launch state is tracked (so onboarding only runs once)
- [ ] Guided prompts cover: panning, zooming, tapping a speech bubble, and completing a quest through to reward
- [ ] Onboarding uses the real game systems (an actual dog, actual quest, actual reward) rather than a separate scripted tutorial scene
- [ ] No blocking modal tutorial screen — guidance is layered over live gameplay
