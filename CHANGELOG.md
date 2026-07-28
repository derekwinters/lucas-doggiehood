# Changelog

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
