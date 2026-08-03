# Changelog

## [0.10.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.9.0...v0.10.0) (2026-08-03)


### Features

* add red finder glow to lost quest items ([#521](https://github.com/derekwinters/lucas-doggiehood/issues/521)) ([#530](https://github.com/derekwinters/lucas-doggiehood/issues/530)) ([c88f7af](https://github.com/derekwinters/lucas-doggiehood/commit/c88f7af41e10217436525ea21f9bcc1eee5a6a2a))
* announce move-ins with the Welcome pop-up ([#528](https://github.com/derekwinters/lucas-doggiehood/issues/528)) ([b1ed96d](https://github.com/derekwinters/lucas-doggiehood/commit/b1ed96d49b16700463db5e13e85f49cd3c8145bf))
* grow the camera max zoom-out with the map extent ([#524](https://github.com/derekwinters/lucas-doggiehood/issues/524)) ([9cbd3fa](https://github.com/derekwinters/lucas-doggiehood/commit/9cbd3fa284bef44f66de8b92e711f9b40297b5ba))
* replace generated house tints with curated 20-color palette ([#519](https://github.com/derekwinters/lucas-doggiehood/issues/519)) ([#531](https://github.com/derekwinters/lucas-doggiehood/issues/531)) ([eac306d](https://github.com/derekwinters/lucas-doggiehood/commit/eac306dea5c97c707ae0c04b351456e2e0b0fedd))


### Bug Fixes

* correct cul-de-sac round-end yaw for the FBX-import mirror ([#514](https://github.com/derekwinters/lucas-doggiehood/issues/514)) ([#525](https://github.com/derekwinters/lucas-doggiehood/issues/525)) ([2667644](https://github.com/derekwinters/lucas-doggiehood/commit/266764431c74c8e04028cd62054ff51e6819528d))
* correct road-bend turn yaws for the FBX-import mirror ([#515](https://github.com/derekwinters/lucas-doggiehood/issues/515)) ([#526](https://github.com/derekwinters/lucas-doggiehood/issues/526)) ([2eafb9c](https://github.com/derekwinters/lucas-doggiehood/commit/2eafb9c1b67bd6dbbecf3f42d524ead398d15ba8))
* hide lost item on the quest dog's own home tile ([#529](https://github.com/derekwinters/lucas-doggiehood/issues/529)) ([960568d](https://github.com/derekwinters/lucas-doggiehood/commit/960568d5893f86cc0533727950da1d04dceb03f0))
* key expansion lock indicators to road connectivity, not grid-adjacency ([#533](https://github.com/derekwinters/lucas-doggiehood/issues/533)) ([9bbd23e](https://github.com/derekwinters/lucas-doggiehood/commit/9bbd23ef56fed6c4bc8dcc639744cf1725d318fb))
* remove unbuildable OpposingTurns tile from the live map ([#516](https://github.com/derekwinters/lucas-doggiehood/issues/516)) ([#527](https://github.com/derekwinters/lucas-doggiehood/issues/527)) ([bb42868](https://github.com/derekwinters/lucas-doggiehood/commit/bb4286813557533858445c5419ba51ba7cf92f14))
* rename Adventurous/Exploring personality to single-word Adventurous ([#532](https://github.com/derekwinters/lucas-doggiehood/issues/532)) ([09a269f](https://github.com/derekwinters/lucas-doggiehood/commit/09a269fad87d8fa607c848b7c8eebb945c2129b4))
* render Tee/turn/cul-de-sac road meshes and derive crosswalks per tile ([#512](https://github.com/derekwinters/lucas-doggiehood/issues/512)) ([621d64c](https://github.com/derekwinters/lucas-doggiehood/commit/621d64ca8831e9f1370a6c87fe0dacd012ea8ac2))
* resident dog at its own door returns down the walkway, not across the yard ([#534](https://github.com/derekwinters/lucas-doggiehood/issues/534)) ([51cb0c2](https://github.com/derekwinters/lucas-doggiehood/commit/51cb0c2a550f745edf97f30f28d415232302124b)), closes [#517](https://github.com/derekwinters/lucas-doggiehood/issues/517)
* suppress onboarding coach bar while a centered modal panel is open ([#522](https://github.com/derekwinters/lucas-doggiehood/issues/522)) ([9eced1a](https://github.com/derekwinters/lucas-doggiehood/commit/9eced1a069749e47105150ff0a1d01d3aa2eab34))

## [0.9.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.8.0...v0.9.0) (2026-08-01)


### Features

* add Debug-tab "Refresh quests now" forced quest rotation ([#487](https://github.com/derekwinters/lucas-doggiehood/issues/487)) ([6bcf4e6](https://github.com/derekwinters/lucas-doggiehood/commit/6bcf4e6c49ebcf0fe3799d154446bf64fd4759e9)), closes [#457](https://github.com/derekwinters/lucas-doggiehood/issues/457)
* allow a manual per-lot fence-anchor override ([#483](https://github.com/derekwinters/lucas-doggiehood/issues/483)) ([6243fb4](https://github.com/derekwinters/lucas-doggiehood/commit/6243fb4e9b2147742a8a54284341be017d9e7532)), closes [#223](https://github.com/derekwinters/lucas-doggiehood/issues/223)
* apply the Candy Cottage chrome to the house and dog profiles ([#495](https://github.com/derekwinters/lucas-doggiehood/issues/495)) ([de5b5e5](https://github.com/derekwinters/lucas-doggiehood/commit/de5b5e5c8b5e684f1e562468c6767770cb9ce0d8)), closes [#465](https://github.com/derekwinters/lucas-doggiehood/issues/465)
* multi-lock frontier expansion, retiring the legacy zone-unlock path ([#503](https://github.com/derekwinters/lucas-doggiehood/issues/503)) ([c7573b8](https://github.com/derekwinters/lucas-doggiehood/commit/c7573b88e5a09683a0ce6eef3a579c5af3637b91)), closes [#453](https://github.com/derekwinters/lucas-doggiehood/issues/453)
* reject a milestone that precedes an issue's blocker milestone ([#481](https://github.com/derekwinters/lucas-doggiehood/issues/481)) ([1843df8](https://github.com/derekwinters/lucas-doggiehood/commit/1843df810fbc52ad8515fbafe5ff4a99e0320c81)), closes [#212](https://github.com/derekwinters/lucas-doggiehood/issues/212)
* render house and resident dog models in the house/dog profiles ([#494](https://github.com/derekwinters/lucas-doggiehood/issues/494)) ([ab48377](https://github.com/derekwinters/lucas-doggiehood/commit/ab483770beb2ca18d452ccd4dbedefd0560be36b)), closes [#464](https://github.com/derekwinters/lucas-doggiehood/issues/464)
* vary yard tree size up to 25% larger, never smaller ([#488](https://github.com/derekwinters/lucas-doggiehood/issues/488)) ([376d793](https://github.com/derekwinters/lucas-doggiehood/commit/376d793a86236e85b86f9ca494ef944aba8c34ba)), closes [#458](https://github.com/derekwinters/lucas-doggiehood/issues/458)


### Bug Fixes

* clip yard trees against a lot's own tile road on cul-de-sac tiles ([#485](https://github.com/derekwinters/lucas-doggiehood/issues/485)) ([7b68deb](https://github.com/derekwinters/lucas-doggiehood/commit/7b68deb20ee87e6f1ca533f421456a5092f3a03e)), closes [#455](https://github.com/derekwinters/lucas-doggiehood/issues/455)
* exclude the lost-puppy subject for puppy dogs ([#493](https://github.com/derekwinters/lucas-doggiehood/issues/493)) ([c1d0426](https://github.com/derekwinters/lucas-doggiehood/commit/c1d0426b28a749c0b4298f159f553072a0322a43)), closes [#463](https://github.com/derekwinters/lucas-doggiehood/issues/463)
* make the delivered gift package drop at the door and be tappable ([#504](https://github.com/derekwinters/lucas-doggiehood/issues/504)) ([ed7d330](https://github.com/derekwinters/lucas-doggiehood/commit/ed7d330aaff01ede6a3eebe9da9b53d43b1dd3e0)), closes [#471](https://github.com/derekwinters/lucas-doggiehood/issues/471)
* onboarding gesture arrows render for every direction and reset the gesture clock on step change ([#498](https://github.com/derekwinters/lucas-doggiehood/issues/498)) ([1f3b735](https://github.com/derekwinters/lucas-doggiehood/commit/1f3b735c3ed887d8f93a35329b0262f07419b31b)), closes [#468](https://github.com/derekwinters/lucas-doggiehood/issues/468)
* orient zone-house fences and yard trees to the real street facing ([#491](https://github.com/derekwinters/lucas-doggiehood/issues/491)) ([282a920](https://github.com/derekwinters/lucas-doggiehood/commit/282a92051461ea06aabd4003602aea963ca4fc9c)), closes [#461](https://github.com/derekwinters/lucas-doggiehood/issues/461)
* re-align an upgraded house to the sidewalk and its front door ([#484](https://github.com/derekwinters/lucas-doggiehood/issues/484)) ([4f2c02d](https://github.com/derekwinters/lucas-doggiehood/commit/4f2c02d8763e9facd3f7b171a9d80abf940e2642)), closes [#454](https://github.com/derekwinters/lucas-doggiehood/issues/454)
* reserve yard trees against the house's max-across-ladder footprint ([#489](https://github.com/derekwinters/lucas-doggiehood/issues/489)) ([65c6a9f](https://github.com/derekwinters/lucas-doggiehood/commit/65c6a9f20d69ae1d46d8047090736cbcd3070b8c)), closes [#459](https://github.com/derekwinters/lucas-doggiehood/issues/459)
* scope onboarding upgrade-house step to the first-quest dog's house ([#500](https://github.com/derekwinters/lucas-doggiehood/issues/500)) ([3c8e903](https://github.com/derekwinters/lucas-doggiehood/commit/3c8e9030be0e9c1384db370d1cda1fc938a0711c)), closes [#469](https://github.com/derekwinters/lucas-doggiehood/issues/469)
* show a contextual reminder when re-tapping a dog with an active quest ([#505](https://github.com/derekwinters/lucas-doggiehood/issues/505)) ([af63833](https://github.com/derekwinters/lucas-doggiehood/commit/af63833aa17fcbbfff8f91ad4447a05070a88060)), closes [#472](https://github.com/derekwinters/lucas-doggiehood/issues/472)
* size backyard-fence connectors from the house's current level ([#490](https://github.com/derekwinters/lucas-doggiehood/issues/490)) ([8edd54d](https://github.com/derekwinters/lucas-doggiehood/commit/8edd54d7fb9c79c50a0fa773d17141984eb75390)), closes [#460](https://github.com/derekwinters/lucas-doggiehood/issues/460)
* stop buy-gift dog moonwalking home and beelining to a stale target ([#502](https://github.com/derekwinters/lucas-doggiehood/issues/502)) ([58c3ad5](https://github.com/derekwinters/lucas-doggiehood/commit/58c3ad50b3e7e4ef5b8a64a12e7263c8285fd6dc)), closes [#470](https://github.com/derekwinters/lucas-doggiehood/issues/470)
* stretch settings sidebar-tab labels so they center and stay in their pill ([#497](https://github.com/derekwinters/lucas-doggiehood/issues/497)) ([ff92c44](https://github.com/derekwinters/lucas-doggiehood/commit/ff92c4434f90c7f088f009dfafc3809e5b4f0c32)), closes [#467](https://github.com/derekwinters/lucas-doggiehood/issues/467)
* wire a freshly built empty house's tap to open its profile ([#486](https://github.com/derekwinters/lucas-doggiehood/issues/486)) ([9906145](https://github.com/derekwinters/lucas-doggiehood/commit/9906145084b35dfd80235c46118bbb67fdf69cbe)), closes [#456](https://github.com/derekwinters/lucas-doggiehood/issues/456)

## [0.8.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.7.0...v0.8.0) (2026-08-01)


### Features

* add animated gesture-arrow coach to onboarding pan/zoom steps ([#445](https://github.com/derekwinters/lucas-doggiehood/issues/445)) ([13d572b](https://github.com/derekwinters/lucas-doggiehood/commit/13d572bb25c821de1adb0e4c21975c5bc1389983)), closes [#330](https://github.com/derekwinters/lucas-doggiehood/issues/330)
* add fence-purchase quest (premium-tier Gift, no-delivery install) ([#443](https://github.com/derekwinters/lucas-doggiehood/issues/443)) ([cb180f0](https://github.com/derekwinters/lucas-doggiehood/commit/cb180f00508b61dbaab8945be2ea3146d40a0d8f)), closes [#318](https://github.com/derekwinters/lucas-doggiehood/issues/318)
* add player-choice frontier tile unlock (flat tunable cost) ([#447](https://github.com/derekwinters/lucas-doggiehood/issues/447)) ([57fe470](https://github.com/derekwinters/lucas-doggiehood/commit/57fe4705e4e920ea9f56fd62168391f0318a5c75)), closes [#295](https://github.com/derekwinters/lucas-doggiehood/issues/295)
* face zone houses to the street with resident-only front walkways ([#449](https://github.com/derekwinters/lucas-doggiehood/issues/449)) ([d913f4d](https://github.com/derekwinters/lucas-doggiehood/commit/d913f4d25c54f1aac534cac905059940ff96ba90)), closes [#430](https://github.com/derekwinters/lucas-doggiehood/issues/430)
* load authored neighborhood map into a validated Core TileMap ([#446](https://github.com/derekwinters/lucas-doggiehood/issues/446)) ([c34c92c](https://github.com/derekwinters/lucas-doggiehood/commit/c34c92ce61c073f92bde6a0c8e49d3291ae3f547)), closes [#383](https://github.com/derekwinters/lucas-doggiehood/issues/383)
* persist the move-in pity counter and easter-egg reserve through SaveCodec ([#474](https://github.com/derekwinters/lucas-doggiehood/issues/474)) ([3170721](https://github.com/derekwinters/lucas-doggiehood/commit/31707216ba8d6c60e586a0ad81cf2f09491ad11e)), closes [#437](https://github.com/derekwinters/lucas-doggiehood/issues/437)
* refine per-tile property lots (bends and cul-de-sacs keep 2 road-facing lots) ([#448](https://github.com/derekwinters/lucas-doggiehood/issues/448)) ([a83a1df](https://github.com/derekwinters/lucas-doggiehood/commit/a83a1df70cd6600f3f30aaad2592de3e48928f46)), closes [#385](https://github.com/derekwinters/lucas-doggiehood/issues/385)
* replace onboarding step-dots with a per-phase coach-bar title tab ([#478](https://github.com/derekwinters/lucas-doggiehood/issues/478)) ([d0475f9](https://github.com/derekwinters/lucas-doggiehood/commit/d0475f91fcac29106d6b9d51f27d349fa75ba9cb)), closes [#451](https://github.com/derekwinters/lucas-doggiehood/issues/451)
* size empty-lot foundations to the predetermined house and pre-place trees ([#450](https://github.com/derekwinters/lucas-doggiehood/issues/450)) ([c96a201](https://github.com/derekwinters/lucas-doggiehood/commit/c96a20193484a7b62cd6dabcdafb662f3fe61c01)), closes [#434](https://github.com/derekwinters/lucas-doggiehood/issues/434)


### Bug Fixes

* match the HUD coins pill height to the settings gear (88px) ([#476](https://github.com/derekwinters/lucas-doggiehood/issues/476)) ([9006175](https://github.com/derekwinters/lucas-doggiehood/commit/9006175685d676963a2d2ff49160292c0a8597ec)), closes [#440](https://github.com/derekwinters/lucas-doggiehood/issues/440)
* reflect move-ins live — spawn the new dog and drop the house greyscale ([#473](https://github.com/derekwinters/lucas-doggiehood/issues/473)) ([c1b07c6](https://github.com/derekwinters/lucas-doggiehood/commit/c1b07c66251a29805321f8371143eeef57db94ab)), closes [#436](https://github.com/derekwinters/lucas-doggiehood/issues/436)

## [0.7.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.6.0...v0.7.0) (2026-07-31)


### Features

* **dashboard:** collapse focus pie to 4 slices (Unplanned/In Planning/Ready/Done) ([#421](https://github.com/derekwinters/lucas-doggiehood/issues/421)) ([7e2c11b](https://github.com/derekwinters/lucas-doggiehood/commit/7e2c11b255737cfdb8f24267fffa9242d35205f0)), closes [#402](https://github.com/derekwinters/lucas-doggiehood/issues/402)
* **onboarding:** celebrate each reward-chain step with the standard reward panel ([#415](https://github.com/derekwinters/lucas-doggiehood/issues/415)) ([f7feb6f](https://github.com/derekwinters/lucas-doggiehood/commit/f7feb6f245b892edb41069a38392ba2a41c0822b)), closes [#372](https://github.com/derekwinters/lucas-doggiehood/issues/372)
* **onboarding:** guide the reward-chain steps with the standard coach bar ([#403](https://github.com/derekwinters/lucas-doggiehood/issues/403)) ([9553cbf](https://github.com/derekwinters/lucas-doggiehood/commit/9553cbfb801fd8718f6b7b476183d7404e195e1f)), closes [#371](https://github.com/derekwinters/lucas-doggiehood/issues/371)
* **ui:** restyle conversation panel to Candy Cottage UGUI ([#426](https://github.com/derekwinters/lucas-doggiehood/issues/426)) ([ea16cc4](https://github.com/derekwinters/lucas-doggiehood/commit/ea16cc41bf118ffea664ddfdd7ecdc50f260d0e5)), closes [#408](https://github.com/derekwinters/lucas-doggiehood/issues/408)


### Bug Fixes

* **expansion:** confirm house build before spending on tap ([#423](https://github.com/derekwinters/lucas-doggiehood/issues/423)) ([10d6f6c](https://github.com/derekwinters/lucas-doggiehood/commit/10d6f6cd9e8bc26b9b90fa75ae579b6357092fb0)), closes [#406](https://github.com/derekwinters/lucas-doggiehood/issues/406)
* **expansion:** make zone 1 a single north cul-de-sac tile ([#399](https://github.com/derekwinters/lucas-doggiehood/issues/399)) ([1694c3e](https://github.com/derekwinters/lucas-doggiehood/commit/1694c3eceb8d29729136d505fa0e6334e8113af0)), closes [#360](https://github.com/derekwinters/lucas-doggiehood/issues/360)
* **expansion:** render ground and roads for unlocked zones and grow camera bounds ([#404](https://github.com/derekwinters/lucas-doggiehood/issues/404)) ([0227d29](https://github.com/derekwinters/lucas-doggiehood/commit/0227d29bf9f558b5f09a4c56a44af8fdc630f826)), closes [#373](https://github.com/derekwinters/lucas-doggiehood/issues/373)
* **expansion:** swap the world house model on upgrade, not just the panel ([#425](https://github.com/derekwinters/lucas-doggiehood/issues/425)) ([47d7b27](https://github.com/derekwinters/lucas-doggiehood/commit/47d7b27e5959106d9670be5b2564362cd7c2fd70)), closes [#407](https://github.com/derekwinters/lucas-doggiehood/issues/407)
* **hud:** draw the settings gear as a procedural Candy Cottage icon ([#401](https://github.com/derekwinters/lucas-doggiehood/issues/401)) ([602ecd3](https://github.com/derekwinters/lucas-doggiehood/commit/602ecd315dc047b8f205f28f5742cd0768df2a4f)), closes [#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)
* **input:** make dialogs modal so taps don't pass through to the world ([#431](https://github.com/derekwinters/lucas-doggiehood/issues/431)) ([60703ef](https://github.com/derekwinters/lucas-doggiehood/commit/60703efe6451ba918e294ca8560b3eb3878ae6dd)), closes [#422](https://github.com/derekwinters/lucas-doggiehood/issues/422)
* **pipeline:** make triage-issue drop ai-triage on hand-back to a single state ([#418](https://github.com/derekwinters/lucas-doggiehood/issues/418)) ([539e6aa](https://github.com/derekwinters/lucas-doggiehood/commit/539e6aa5d2b7cb74b2e6bea12d4a81bd5d44b93d)), closes [#394](https://github.com/derekwinters/lucas-doggiehood/issues/394)
* **pipeline:** resolve wireframe blockers only when closed, ending revisit churn ([#419](https://github.com/derekwinters/lucas-doggiehood/issues/419)) ([27aef5d](https://github.com/derekwinters/lucas-doggiehood/commit/27aef5ddc94b6128db96417557a1f84800efc90c)), closes [#396](https://github.com/derekwinters/lucas-doggiehood/issues/396)
* resolve zone house models through rolled ladder in HouseModelCatalog.ForHouse ([#428](https://github.com/derekwinters/lucas-doggiehood/issues/428)) ([19bead2](https://github.com/derekwinters/lucas-doggiehood/commit/19bead260c624eadca8d73fabd549235e5145033)), closes [#414](https://github.com/derekwinters/lucas-doggiehood/issues/414)
* **world:** derive the wander walk network from the live multi-tile map ([#420](https://github.com/derekwinters/lucas-doggiehood/issues/420)) ([f12205b](https://github.com/derekwinters/lucas-doggiehood/commit/f12205b2308b9370a59e4e649ea9b46e32abf1ef)), closes [#398](https://github.com/derekwinters/lucas-doggiehood/issues/398)
* **world:** extend starting road arms to the tile edge so tiles connect on expansion ([#417](https://github.com/derekwinters/lucas-doggiehood/issues/417)) ([5ff6e0d](https://github.com/derekwinters/lucas-doggiehood/commit/5ff6e0d49d799b1871b5d9553ad2bde20de7bdb0)), closes [#392](https://github.com/derekwinters/lucas-doggiehood/issues/392)
* **world:** fence every built house, including zone lots ([#433](https://github.com/derekwinters/lucas-doggiehood/issues/433)) ([946b6dd](https://github.com/derekwinters/lucas-doggiehood/commit/946b6ddd403c8a3cb392a154d1022919236a34dd)), closes [#424](https://github.com/derekwinters/lucas-doggiehood/issues/424)
* **world:** render walkway/yard/fence for mid-game zone-lot builds ([#429](https://github.com/derekwinters/lucas-doggiehood/issues/429)) ([ab04583](https://github.com/derekwinters/lucas-doggiehood/commit/ab04583030acae8ecf207389c0bc15665f783689)), closes [#405](https://github.com/derekwinters/lucas-doggiehood/issues/405)

## [0.6.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.5.0...v0.6.0) (2026-07-30)


### Features

* add 4-step onboarding reward-chain ([#365](https://github.com/derekwinters/lucas-doggiehood/issues/365)) ([174e147](https://github.com/derekwinters/lucas-doggiehood/commit/174e1478d7f1e34d39e8427c14d8cdb556051085))
* add house profile view (level, residents, upgrade entry point) ([#355](https://github.com/derekwinters/lucas-doggiehood/issues/355)) ([9a9c047](https://github.com/derekwinters/lucas-doggiehood/commit/9a9c047758b1f58a10e0b53d21e2e70dc6b896a6))
* add HouseModelCatalog rows for 5th-ladder meshes o/p/a ([#387](https://github.com/derekwinters/lucas-doggiehood/issues/387)) ([0f42d8b](https://github.com/derekwinters/lucas-doggiehood/commit/0f42d8b0615e52a2ad3a2430f9049048187c472a)), closes [#348](https://github.com/derekwinters/lucas-doggiehood/issues/348)
* gate quest cost tiers by dog population ([#366](https://github.com/derekwinters/lucas-doggiehood/issues/366)) ([81f622f](https://github.com/derekwinters/lucas-doggiehood/commit/81f622f74f484ac8748cc529dd2584073d32274f))
* give approach-to-rest real walk-to-decoration movement ([#353](https://github.com/derekwinters/lucas-doggiehood/issues/353)) ([580db4c](https://github.com/derekwinters/lucas-doggiehood/commit/580db4c4240eb6345a31f154a0e9959e58a2f9fd))
* open a dog profile view when tapping a dog ([#354](https://github.com/derekwinters/lucas-doggiehood/issues/354)) ([f130c66](https://github.com/derekwinters/lucas-doggiehood/commit/f130c6698a1ae4b0f431310917ccb9101c7e7dc9))
* **pipeline:** fire analysis Routine reactively when an issue gains ai-triage ([#379](https://github.com/derekwinters/lucas-doggiehood/issues/379)) ([b2ce425](https://github.com/derekwinters/lucas-doggiehood/commit/b2ce425c271ac09994dcb1966bddee83b2860ac9))
* render lost-item puppy with the shared dog model ([#388](https://github.com/derekwinters/lucas-doggiehood/issues/388)) ([4079599](https://github.com/derekwinters/lucas-doggiehood/commit/4079599b2f7264a6b8347b46644072340d8860c2)), closes [#335](https://github.com/derekwinters/lucas-doggiehood/issues/335)
* restyle empty-lot marker as a raised foundation slab ([#390](https://github.com/derekwinters/lucas-doggiehood/issues/390)) ([1af926c](https://github.com/derekwinters/lucas-doggiehood/commit/1af926cc064268ec48aec72454398978667b8eee)), closes [#300](https://github.com/derekwinters/lucas-doggiehood/issues/300)
* restyle HUD currency chip to Candy Cottage chrome ([#361](https://github.com/derekwinters/lucas-doggiehood/issues/361)) ([1625bdc](https://github.com/derekwinters/lucas-doggiehood/commit/1625bdc59bf48b934d2075217d63b02d5d165e98))
* restyle onboarding overlay to Candy Cottage chrome ([#362](https://github.com/derekwinters/lucas-doggiehood/issues/362)) ([4566679](https://github.com/derekwinters/lucas-doggiehood/commit/456667951e3d6a72d53cd7fdd99dca69f73be102))
* restyle settings panel to Candy Cottage chrome ([#363](https://github.com/derekwinters/lucas-doggiehood/issues/363)) ([f5f872b](https://github.com/derekwinters/lucas-doggiehood/commit/f5f872bb1db7dadfe074f56226d9bb79fa939ab7))
* wire map-expansion unlock trigger to the tappable lock icon ([#367](https://github.com/derekwinters/lucas-doggiehood/issues/367)) ([5919eb1](https://github.com/derekwinters/lucas-doggiehood/commit/5919eb18894d8da09a0f7c203ac0f1246792f1c6))
* **world:** real art pass for zone-built houses (rolled ladder + palette tint) ([#391](https://github.com/derekwinters/lucas-doggiehood/issues/391)) ([240b711](https://github.com/derekwinters/lucas-doggiehood/commit/240b7113d683a661348c3d3dc1ca673c2fb76fbc)), closes [#299](https://github.com/derekwinters/lucas-doggiehood/issues/299)


### Bug Fixes

* attach release APK from the release-please run ([#386](https://github.com/derekwinters/lucas-doggiehood/issues/386)) ([d3d5b88](https://github.com/derekwinters/lucas-doggiehood/commit/d3d5b8817f2bbff6a9dcb65e813a270f201a749f))
* **pipeline:** persist /focus (and /cap) by re-rendering the dashboard ([#350](https://github.com/derekwinters/lucas-doggiehood/issues/350)) ([aa421e3](https://github.com/derekwinters/lucas-doggiehood/commit/aa421e3003b843424f3f56b32c52f76c86bbb3a0))
* **pipeline:** validate the routine_fire response so triage fires aren't false positives ([#381](https://github.com/derekwinters/lucas-doggiehood/issues/381)) ([26316bb](https://github.com/derekwinters/lucas-doggiehood/commit/26316bbba62a097903025b75e41063973a286388))
* **world:** offset backyard fences from the sidewalk on road-bordering lot edges ([#389](https://github.com/derekwinters/lucas-doggiehood/issues/389)) ([e5998a4](https://github.com/derekwinters/lucas-doggiehood/commit/e5998a42f76e916627e5113efcbc649f06479e55)), closes [#147](https://github.com/derekwinters/lucas-doggiehood/issues/147)

## [0.5.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.4.0...v0.5.0) (2026-07-28)


### Features

* add "Add coins" debug action to the settings Debug tab ([#306](https://github.com/derekwinters/lucas-doggiehood/issues/306)) ([ca5cf89](https://github.com/derekwinters/lucas-doggiehood/commit/ca5cf89c4b361393f227d9e7fd6a2a71728d371c)), closes [#286](https://github.com/derekwinters/lucas-doggiehood/issues/286)
* add /cap command to configure the nightly dev build limit ([#314](https://github.com/derekwinters/lucas-doggiehood/issues/314)) ([8e1ee2b](https://github.com/derekwinters/lucas-doggiehood/commit/8e1ee2b2aaa1af6a015a564329d4c9024e303479))
* **dashboard:** add a Milestone column to every issue table ([#337](https://github.com/derekwinters/lucas-doggiehood/issues/337)) ([3ad77b0](https://github.com/derekwinters/lucas-doggiehood/commit/3ad77b040c9db51539c14f16b98f37b3bcad2eb4))
* expand dashboard focus pie to a 7-slice pipeline-state breakdown ([#315](https://github.com/derekwinters/lucas-doggiehood/issues/315)) ([4d78c08](https://github.com/derekwinters/lucas-doggiehood/commit/4d78c08dfe9c05ea926118411ff6b21f738411fa))
* house leveling system — upgrade 1→4, decoration cap, model-swap visuals ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)) ([#302](https://github.com/derekwinters/lucas-doggiehood/issues/302)) ([a56119d](https://github.com/derekwinters/lucas-doggiehood/commit/a56119da1f501ed49658ee0b2c727692b3d96600))
* **pipeline:** read native GitHub issue-dependency blockers alongside the text line ([#346](https://github.com/derekwinters/lucas-doggiehood/issues/346)) ([84a2afd](https://github.com/derekwinters/lucas-doggiehood/commit/84a2afd341754dcddd9b94ef22c432abbfcb1657))
* seed one easy lost-item quest on first launch, suppress rotation until onboarding completes ([#323](https://github.com/derekwinters/lucas-doggiehood/issues/323)) ([92f360c](https://github.com/derekwinters/lucas-doggiehood/commit/92f360cce0c0b769bd9c7cb16faf152492da5efb))
* show blockers on every dashboard section and auto-revisit unblocked questions ([#304](https://github.com/derekwinters/lucas-doggiehood/issues/304)) ([4586238](https://github.com/derekwinters/lucas-doggiehood/commit/4586238dd2c05ca1d0910494d4701085330066a2)), closes [#241](https://github.com/derekwinters/lucas-doggiehood/issues/241)
* **ui:** simplify the About pane credit to "Designed by Lucas" ([#339](https://github.com/derekwinters/lucas-doggiehood/issues/339)) ([4913697](https://github.com/derekwinters/lucas-doggiehood/commit/4913697cc77a86ad34d4b211d1935d566d669a5a))
* **world:** widen backyard fences to trace the lot boundary ([#342](https://github.com/derekwinters/lucas-doggiehood/issues/342)) ([c97902d](https://github.com/derekwinters/lucas-doggiehood/commit/c97902daeab0a4b9e0c73e62d3aa23d8222ca197))


### Bug Fixes

* face world markers to live camera yaw under free rotation ([#305](https://github.com/derekwinters/lucas-doggiehood/issues/305)) ([ab7e04a](https://github.com/derekwinters/lucas-doggiehood/commit/ab7e04afab6e079f23d25908378107902804ce48)), closes [#266](https://github.com/derekwinters/lucas-doggiehood/issues/266)
* keep lost-item spawns clear of house footprints ([#307](https://github.com/derekwinters/lucas-doggiehood/issues/307)) ([d8372d4](https://github.com/derekwinters/lucas-doggiehood/commit/d8372d4500fbb2d614e4dedc5acbf91002179f48)), closes [#290](https://github.com/derekwinters/lucas-doggiehood/issues/290)
* **onboarding:** gate the speech-bubble step and self-heal if it was already done ([#340](https://github.com/derekwinters/lucas-doggiehood/issues/340)) ([b29d1f9](https://github.com/derekwinters/lucas-doggiehood/commit/b29d1f9315f1cf95798f7ee13e98e5bc13cefda6))
* **quests:** make the pest-quest house indicator clearly visible ([#341](https://github.com/derekwinters/lucas-doggiehood/issues/341)) ([799940a](https://github.com/derekwinters/lucas-doggiehood/commit/799940aa1aac11868712e3b18ea9d9f2f3783899))
* retain UI/Default shader and bundle a font so the settings panel renders in builds ([#308](https://github.com/derekwinters/lucas-doggiehood/issues/308)) ([721d9ce](https://github.com/derekwinters/lucas-doggiehood/commit/721d9cec75cc59527441b8bb9d0e895a7067aecb)), closes [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)
* route real lost-item taps through a padded screen-space tap zone ([#322](https://github.com/derekwinters/lucas-doggiehood/issues/322)) ([edcaeea](https://github.com/derekwinters/lucas-doggiehood/commit/edcaeeacdb24d2311f4828274d5e48b42f2254d6))
* **ui:** create an EventSystem at bootstrap so Settings UGUI controls receive taps ([#338](https://github.com/derekwinters/lucas-doggiehood/issues/338)) ([c3161cf](https://github.com/derekwinters/lucas-doggiehood/commit/c3161cfc69febadb237d8f7637b748788ccd5745))

## [0.4.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.3.0...v0.4.0) (2026-07-25)


### Features

* add pipeline reconciliation sweep for drifted issues ([#276](https://github.com/derekwinters/lucas-doggiehood/issues/276)) ([2a027c7](https://github.com/derekwinters/lucas-doggiehood/commit/2a027c76d373991361ee000356572027d2acb037))
* add Settings menu with About tab, Debug unlock, and fence toggle ([#289](https://github.com/derekwinters/lucas-doggiehood/issues/289)) ([0d57586](https://github.com/derekwinters/lucas-doggiehood/commit/0d5758653efa06379ba6bbc64436d2f836db28a2))
* **ai:** adopt shared ai-skills bundles + /focus fix ([#238](https://github.com/derekwinters/lucas-doggiehood/issues/238)) ([7c8f6fd](https://github.com/derekwinters/lucas-doggiehood/commit/7c8f6fd8f91e3e9f78273b95902ad8b0c2218d05))
* **camera:** add free twist-driven yaw rotation ([#264](https://github.com/derekwinters/lucas-doggiehood/issues/264)) ([cfca215](https://github.com/derekwinters/lucas-doggiehood/commit/cfca2151d35134442110ee3c63698e18d03dd7b1))
* **dashboard:** add read-only Parked section ([#282](https://github.com/derekwinters/lucas-doggiehood/issues/282)) ([04c743a](https://github.com/derekwinters/lucas-doggiehood/commit/04c743ac066ee55e54ae2d9226e843cc2bc5bf79))
* **dashboard:** star unblocking issues in the ready-for-work queue ([#283](https://github.com/derekwinters/lucas-doggiehood/issues/283)) ([b2d282e](https://github.com/derekwinters/lucas-doggiehood/commit/b2d282efe54cf3581bc5d37885182871112c8249))
* drop release-please parenthetical from dashboard Your move PR count ([#253](https://github.com/derekwinters/lucas-doggiehood/issues/253)) ([a3f348f](https://github.com/derekwinters/lucas-doggiehood/commit/a3f348f44d2f509e3f77090bd32231fcbea5aa51))
* enlarge conversation panel dialogue text and buttons for readability ([#274](https://github.com/derekwinters/lucas-doggiehood/issues/274)) ([4d85d47](https://github.com/derekwinters/lucas-doggiehood/commit/4d85d471f867124b6d256e798dfebe3480c4edee))
* **expansion:** move dogs into empty houses over time via a pity ([e355cfb](https://github.com/derekwinters/lucas-doggiehood/commit/e355cfb817323cb08606f44097e7510783a9e261))
* **expansion:** move dogs into empty houses over time via a pity counter ([#237](https://github.com/derekwinters/lucas-doggiehood/issues/237)) ([e355cfb](https://github.com/derekwinters/lucas-doggiehood/commit/e355cfb817323cb08606f44097e7510783a9e261))
* **expansion:** tint the map-expansion lock indicator by affordability ([#261](https://github.com/derekwinters/lucas-doggiehood/issues/261)) ([966d346](https://github.com/derekwinters/lucas-doggiehood/commit/966d34605d76646e1357d6e3456af5a7cac53738))
* **expansion:** unlock authored map zones with a currency cost ([#239](https://github.com/derekwinters/lucas-doggiehood/issues/239)) ([d71f56c](https://github.com/derekwinters/lucas-doggiehood/commit/d71f56c64b45235b8c40021b74cd5e773d2fc957))
* **expansion:** unlock authored map zones with a currency cost ([#56](https://github.com/derekwinters/lucas-doggiehood/issues/56)) ([d71f56c](https://github.com/derekwinters/lucas-doggiehood/commit/d71f56c64b45235b8c40021b74cd5e773d2fc957))
* **quests:** add a Not-now decline action to the conversation panel ([#262](https://github.com/derekwinters/lucas-doggiehood/issues/262)) ([865eadd](https://github.com/derekwinters/lucas-doggiehood/commit/865eadd174bd6fafb6e9b0e5552702b9a1126765))
* **quests:** show cost and insufficient-funds feedback in the conversation panel ([#263](https://github.com/derekwinters/lucas-doggiehood/issues/263)) ([c47c045](https://github.com/derekwinters/lucas-doggiehood/commit/c47c045a471509e2d5b1311cb65ee74a1a5b2f11)), closes [#186](https://github.com/derekwinters/lucas-doggiehood/issues/186)
* **reconcile:** flag prose-only dependency references ([#281](https://github.com/derekwinters/lucas-doggiehood/issues/281)) ([67df8c5](https://github.com/derekwinters/lucas-doggiehood/commit/67df8c5990781fafd9eb6fcf3beffa963b8babd7))
* **ui:** lock landscape orientation and add 1920x1200 CanvasScaler ([#285](https://github.com/derekwinters/lucas-doggiehood/issues/285)) ([35716ce](https://github.com/derekwinters/lucas-doggiehood/commit/35716ce560c8f63156ee25e6af7101ad4dadbfdb))
* **world:** define lot bounds as a tile-quadrant primitive (refs [#222](https://github.com/derekwinters/lucas-doggiehood/issues/222)) ([79a5427](https://github.com/derekwinters/lucas-doggiehood/commit/79a54279212df98e090d6bde40caf7b00a40fb12))
* **world:** define lot bounds as a tile-quadrant primitive (refs [#222](https://github.com/derekwinters/lucas-doggiehood/issues/222)) ([#242](https://github.com/derekwinters/lucas-doggiehood/issues/242)) ([79a5427](https://github.com/derekwinters/lucas-doggiehood/commit/79a54279212df98e090d6bde40caf7b00a40fb12))


### Bug Fixes

* **ci:** stop docs-test gate flapping red on skip-docs PRs ([#284](https://github.com/derekwinters/lucas-doggiehood/issues/284)) ([c4add0c](https://github.com/derekwinters/lucas-doggiehood/commit/c4add0c8758e9c5c79f9735d6fc351cb2e6fed5d))
* clear the perpendicular road corridor from yard regions too ([#275](https://github.com/derekwinters/lucas-doggiehood/issues/275)) ([46060c2](https://github.com/derekwinters/lucas-doggiehood/commit/46060c2f0f554fb8e185c9a50dc88f448d3c83a3))
* conversation panel resolves one quest at a time and is always dismissable ([#269](https://github.com/derekwinters/lucas-doggiehood/issues/269)) ([45e1772](https://github.com/derekwinters/lucas-doggiehood/commit/45e1772cbd08aeac6a56dc365e48608e3e75f49a))
* **dashboard:** hide closed milestones from the pipeline dashboard ([#251](https://github.com/derekwinters/lucas-doggiehood/issues/251)) ([f68af19](https://github.com/derekwinters/lucas-doggiehood/commit/f68af197168d6d72ba3f97622d552a4251d98b02))
* **gatekeeper:** refuse /approve to ready-for-work without a milestone ([#280](https://github.com/derekwinters/lucas-doggiehood/issues/280)) ([528034d](https://github.com/derekwinters/lucas-doggiehood/commit/528034d5df345bdce648536e528874cca1d2fa7e))
* honor /focus on the dashboard issue so focus can be set from [#193](https://github.com/derekwinters/lucas-doggiehood/issues/193) ([#226](https://github.com/derekwinters/lucas-doggiehood/issues/226)) ([b5e90aa](https://github.com/derekwinters/lucas-doggiehood/commit/b5e90aa8f8c6606fe814c0597cd851f339f1c1f7))
* keep procedural yard trees out of the road ([#271](https://github.com/derekwinters/lucas-doggiehood/issues/271)) ([5f7c7ed](https://github.com/derekwinters/lucas-doggiehood/commit/5f7c7ed0cb3aaed7a2fe587cb02d2fa6328c2a14))
* make onboarding coach prompt advance and dismiss, re-lay against wireframe ([#268](https://github.com/derekwinters/lucas-doggiehood/issues/268)) ([20e8bbb](https://github.com/derekwinters/lucas-doggiehood/commit/20e8bbb222da399ee62a965fbd0d5f7f645a91fd))
* reconcile done-ness counts only closing-keyword refs ([#278](https://github.com/derekwinters/lucas-doggiehood/issues/278)) ([1f0f099](https://github.com/derekwinters/lucas-doggiehood/commit/1f0f0999665e6e6b19f3f7f47ac2e6c78126fa88))
* remove planters from procedural yard landscaping ([#270](https://github.com/derekwinters/lucas-doggiehood/issues/270)) ([37a374d](https://github.com/derekwinters/lucas-doggiehood/commit/37a374d678bfa13581c85bc1380adf9187efdd7d))
* set /focus by re-rendering the dashboard, never hand-editing [#193](https://github.com/derekwinters/lucas-doggiehood/issues/193) ([#230](https://github.com/derekwinters/lucas-doggiehood/issues/230)) ([ef5e8a3](https://github.com/derekwinters/lucas-doggiehood/commit/ef5e8a36d09dd05df5dc92bcb474ff2f5a1b8a61))

## [0.3.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.2.0...v0.3.0) (2026-07-19)


### Features

* add morning-report skill for repo status summaries ([#164](https://github.com/derekwinters/lucas-doggiehood/issues/164)) ([05ffe06](https://github.com/derekwinters/lucas-doggiehood/commit/05ffe061ee12a6854b685f9aeacc73706369f0f3))
* add pipeline-gatekeeper skill — owner-only comment→label parser ([ba2c37c](https://github.com/derekwinters/lucas-doggiehood/commit/ba2c37c9fde259104025bcce0acc9646026476d8))
* add the AI issue-management pipeline (skills, dashboard workflow, docs) ([#202](https://github.com/derekwinters/lucas-doggiehood/issues/202)) ([ba2c37c](https://github.com/derekwinters/lucas-doggiehood/commit/ba2c37c9fde259104025bcce0acc9646026476d8))
* apply kit model + tint palette to rendered houses ([#168](https://github.com/derekwinters/lucas-doggiehood/issues/168)) ([c5280c7](https://github.com/derekwinters/lucas-doggiehood/commit/c5280c7a9527a16cdffe773591167c7c8fa49177))
* **build:** adopt the cover art and show it at app launch ([#133](https://github.com/derekwinters/lucas-doggiehood/issues/133)) ([a083950](https://github.com/derekwinters/lucas-doggiehood/commit/a083950d5eb7b3dcc7d300174d32d7b9b86e8be8))
* **build:** apply .debug applicationId suffix for debug builds ([#115](https://github.com/derekwinters/lucas-doggiehood/issues/115)) ([d8107c3](https://github.com/derekwinters/lucas-doggiehood/commit/d8107c3ed614ec804d85bbf8ff06c53a2f17dcd8))
* **build:** set Doggiehood app icon replacing the Unity default ([#132](https://github.com/derekwinters/lucas-doggiehood/issues/132)) ([1a2468c](https://github.com/derekwinters/lucas-doggiehood/commit/1a2468c7d4be1df6033ec8002c3bf79fefdbf251))
* **dogs:** use Kenney Cube Pets model as shared placeholder dog visual ([#123](https://github.com/derekwinters/lucas-doggiehood/issues/123)) ([43ff8f7](https://github.com/derekwinters/lucas-doggiehood/commit/43ff8f740c44b8177d96be4a841f04f94d25b342))
* **editor:** add a procedurally built catalog gallery scene for authoring house values ([#141](https://github.com/derekwinters/lucas-doggiehood/issues/141)) ([ee3dd2d](https://github.com/derekwinters/lucas-doggiehood/commit/ee3dd2d5a12d0091b0d9843144c33415971f8252))
* **expansion:** stage lock-icon map-expansion indicator ([#183](https://github.com/derekwinters/lucas-doggiehood/issues/183)) ([dc77798](https://github.com/derekwinters/lucas-doggiehood/commit/dc77798a313828c0fbc395c41a9eb60837c3a84c))
* make the bug-problem quest visible and verify all 3 fulfillment flows end-to-end ([#157](https://github.com/derekwinters/lucas-doggiehood/issues/157)) ([#179](https://github.com/derekwinters/lucas-doggiehood/issues/179)) ([0e3f49c](https://github.com/derekwinters/lucas-doggiehood/commit/0e3f49c7ed5f2e83b2f26b42d903c99e5181766e))
* **quests:** pool opener/closer lines with uniform-random selection ([87b1c1e](https://github.com/derekwinters/lucas-doggiehood/commit/87b1c1e1987943b2460522f44aef2e4022f1b564))
* **quests:** pool opener/closer lines with uniform-random selection ([#215](https://github.com/derekwinters/lucas-doggiehood/issues/215)) ([87b1c1e](https://github.com/derekwinters/lucas-doggiehood/commit/87b1c1e1987943b2460522f44aef2e4022f1b564))
* require a TDD Build checklist on every pipeline-analysis plan ([#206](https://github.com/derekwinters/lucas-doggiehood/issues/206)) ([bdcfbbf](https://github.com/derekwinters/lucas-doggiehood/commit/bdcfbbf61ac782c94b45bccec3134fff145fb874))
* **ui:** show the live coin balance in a graybox HUD currency chip ([#162](https://github.com/derekwinters/lucas-doggiehood/issues/162)) ([ff9bbcd](https://github.com/derekwinters/lucas-doggiehood/commit/ff9bbcda1719c2417a0a10d9e76a8fd87605d6bb))
* **world:** add a Core house-model catalog with footprints and front-door positions ([#140](https://github.com/derekwinters/lucas-doggiehood/issues/140)) ([9e8f82f](https://github.com/derekwinters/lucas-doggiehood/commit/9e8f82fbd94dfe2fc8df4774a752cb294da47a55)), closes [#125](https://github.com/derekwinters/lucas-doggiehood/issues/125)
* **world:** add lot fences with a gate gap at the front walkway ([#144](https://github.com/derekwinters/lucas-doggiehood/issues/144)) ([40338bb](https://github.com/derekwinters/lucas-doggiehood/commit/40338bb49987945d35d78a8f7e459da44fbaad1b))
* **world:** lock standard world dimensions and add tile catalog design doc ([#110](https://github.com/derekwinters/lucas-doggiehood/issues/110)) ([0edd9ad](https://github.com/derekwinters/lucas-doggiehood/commit/0edd9ad95f238a7eca31570d4bb22c105b0c4319)), closes [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105)
* **world:** place houses at a front setback from their street's sidewalk ([#142](https://github.com/derekwinters/lucas-doggiehood/issues/142)) ([831f867](https://github.com/derekwinters/lucas-doggiehood/commit/831f8672064faae77d099795b6ed38041893c371))
* **world:** replace driveway stubs with front walkways from door to sidewalk ([#143](https://github.com/derekwinters/lucas-doggiehood/issues/143)) ([545e2e2](https://github.com/derekwinters/lucas-doggiehood/commit/545e2e2132d47a7770750787afc021b2336aa239))
* **world:** replace graybox roads and houses with Kenney City Kit models ([#124](https://github.com/derekwinters/lucas-doggiehood/issues/124)) ([958dfd3](https://github.com/derekwinters/lucas-doggiehood/commit/958dfd3807605c48b9fe5ff41d13cfa5ee0c87e2))
* **world:** reshape fences into hidden-by-default backyard enclosures ([#153](https://github.com/derekwinters/lucas-doggiehood/issues/153)) ([cd500c5](https://github.com/derekwinters/lucas-doggiehood/commit/cd500c5dc939cf7269175fb5b9b4a43ed68d3c09))
* **world:** scale every house model by one fixed ×7 kit scale ([#150](https://github.com/derekwinters/lucas-doggiehood/issues/150)) ([8ec659b](https://github.com/derekwinters/lucas-doggiehood/commit/8ec659b32eb9238eeddc71508b65b3123c515a5d))
* **world:** sidewalks, crosswalks, and sidewalk-only walking ([#113](https://github.com/derekwinters/lucas-doggiehood/issues/113)) ([d9a8606](https://github.com/derekwinters/lucas-doggiehood/commit/d9a8606c5ba442359126d398b3158e4a25fc1d29))


### Bug Fixes

* **ci:** exempt release-please's release PR from the docs-reconciliation gate ([#216](https://github.com/derekwinters/lucas-doggiehood/issues/216)) ([2734105](https://github.com/derekwinters/lucas-doggiehood/commit/273410566ed69b8f1db7a0cea96fadf68acffcae))
* **ci:** fail EditMode job when the Unity license secret is missing ([7ff46e7](https://github.com/derekwinters/lucas-doggiehood/commit/7ff46e75fad3001303e27ceb15d9682c98a0d0b6))
* **ci:** fail EditMode job when the Unity license secret is missing ([#210](https://github.com/derekwinters/lucas-doggiehood/issues/210)) ([7ff46e7](https://github.com/derekwinters/lucas-doggiehood/commit/7ff46e75fad3001303e27ceb15d9682c98a0d0b6))
* pin activeInputHandler to the legacy Input Manager so input works at all ([#139](https://github.com/derekwinters/lucas-doggiehood/issues/139)) ([31aa098](https://github.com/derekwinters/lucas-doggiehood/commit/31aa0980b9305bba80c14b42b1a07cc04ab4d742))
* **release:** make release-please's extra-files updater actually bump VERSION ([#117](https://github.com/derekwinters/lucas-doggiehood/issues/117)) ([a81d15c](https://github.com/derekwinters/lucas-doggiehood/commit/a81d15c928ac7338a8978853e1ff2ea43538cc34))
* **ui:** make dogs and speech bubbles tappable, and polish the bubble's size, height, and facing ([#158](https://github.com/derekwinters/lucas-doggiehood/issues/158)) ([e34db0b](https://github.com/derekwinters/lucas-doggiehood/commit/e34db0bc001249763302a07a1d1871f974a8c26a))
* **world:** record the gallery-authored front-door positions as 2D model-local points ([#149](https://github.com/derekwinters/lucas-doggiehood/issues/149)) ([71378f7](https://github.com/derekwinters/lucas-doggiehood/commit/71378f7f7acc66dee4153001d2aa2676f84d173f))
* **world:** resolve dog ground height from sidewalk vs road surface ([90a77f0](https://github.com/derekwinters/lucas-doggiehood/commit/90a77f0e90778a85cc7bb9c7a5247a08db4c7262))
* **world:** resolve dog ground height from sidewalk vs road surface ([#213](https://github.com/derekwinters/lucas-doggiehood/issues/213)) ([90a77f0](https://github.com/derekwinters/lucas-doggiehood/commit/90a77f0e90778a85cc7bb9c7a5247a08db4c7262))

## [0.2.0](https://github.com/derekwinters/lucas-doggiehood/compare/v0.1.0...v0.2.0) (2026-07-12)


### Features

* mvp foundations — milestones 01-05 plus onboarding and audio wiring ([#93](https://github.com/derekwinters/lucas-doggiehood/issues/93)) ([3368fec](https://github.com/derekwinters/lucas-doggiehood/commit/3368fecec14d69e24503cfeb1eadcd582a487d7f))
