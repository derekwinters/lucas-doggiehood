# World Art

*Epic: [#795](https://github.com/derekwinters/lucas-doggiehood/issues/795). Spec: [#800](https://github.com/derekwinters/lucas-doggiehood/issues/800). Decisions: [#796](https://github.com/derekwinters/lucas-doggiehood/issues/796), [#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)*

The world around the houses is being upgraded along with them: textured lawns, roads and sidewalks, textured trees, a small set of street props, and fences. Everything follows the [art direction](art-style.md#art-direction): clean, stylized 3D with realistic colours and real textures, where textures carry the detail rather than polygon count. Houses have their own page, [House Design](house-design.md).

!!! warning "This is the contract, not yet the code"
    The game still renders the flat grass colour, the Kenney roads, sidewalks, fences and yard trees described in [World & Neighborhood](world.md) and [Sidewalks & Walk Network](sidewalks.md) until the implementation issues land ([#808](https://github.com/derekwinters/lucas-doggiehood/issues/808)–[#813](https://github.com/derekwinters/lucas-doggiehood/issues/813)). No world implementation starts before Derek approves every world model on [#807](https://github.com/derekwinters/lucas-doggiehood/issues/807).

## How the models are made

Every world model is a Blender Python script under `art-source/blender/`, built by Claude and reviewed by Derek, using the same [Blender asset pipeline](../../engineering/blender-pipeline.md) as the houses (conventions **Open** on [#774](https://github.com/derekwinters/lucas-doggiehood/issues/774) and [#775](https://github.com/derekwinters/lucas-doggiehood/issues/775)). Every model stays within the [art budget](art-style.md#art-budget).

**Textures are shared, not copied.** Each textured set exports as `.gltf` + `.bin` with its images referenced from one shared `textures/` folder, so each texture is stored once. The approved trees ([#816](https://github.com/derekwinters/lucas-doggiehood/issues/816)) and street props ([#821](https://github.com/derekwinters/lucas-doggiehood/issues/821)) are built this way under `art-source/blender/environment/`.

**Reference-only assets.** Some approved models are kept as source and reference designs for a consistent style, but are not used in the game ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)). They stay outside `Assets/`.

**Invariant — reference-only assets never ship in the build.** Only models this page lists as in use are imported under `Assets/`. A reference-only model is promoted by changing this page first, not by importing it.

**Invariant — a world model's footprint matches the tile or lot geometry it replaces exactly.** A new road, sidewalk, walkway or fence model keeps today's widths, offsets and positions, so placement code, walk paths and truck paths do not change.

## Ground: lawns

Every lot gets a **textured lawn**, a tileable grass texture rather than today's flat grass colour.

**Open** ([#801](https://github.com/derekwinters/lucas-doggiehood/issues/801), [#808](https://github.com/derekwinters/lucas-doggiehood/issues/808)): the lawn texture itself (colour, scale in metres, how visible repeats are hidden); whether the grass verge and the [open-space](world.md#open-space-environment) fill use the same lawn; and how lot edges meet sidewalks and fences.

**Later:** mowing lawns as a chore, with grass that looks lightly overgrown when due, is an idea for the future and not part of this page ([#820](https://github.com/derekwinters/lucas-doggiehood/issues/820)).

## Roads

The Kenney road tiles are replaced by a Blender road kit covering every road tile type in the [Tile Catalog](tile-catalog.md). It keeps today's road width (6 m), the sidewalk offsets and the crosswalk positions exactly.

**Open** ([#802](https://github.com/derekwinters/lucas-doggiehood/issues/802)): the asphalt texture, lane paint, curbs and crosswalk markings. The approved street-prop road decals (crosswalk, stop line, manhole cover, storm drain) are reference only.

## Sidewalks, front walkways and driveways

Sidewalks and front walkways keep every width and position in [Sidewalks & Walk Network](sidewalks.md) (sidewalk 2 m, grass verge 0.75 m).

**Driveways.** A house with a [garage](house-design.md#garage-level-2) gets a **driveway**: ground decoration in front of the garage only ([#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)). It adds **no walk-network edge**. Dogs still reach the front door by the front walkway, which stays the only way on or off a lot ([#128](https://github.com/derekwinters/lucas-doggiehood/issues/128)).

**Open** ([#803](https://github.com/derekwinters/lucas-doggiehood/issues/803)): the concrete surfaces and joints, and the driveway's exact shape and size.

## Trees

Trees use the approved **textured** technique ([#816](https://github.com/derekwinters/lucas-doggiehood/issues/816)): each crown is a few hundred alpha-cut leaf cards carrying painted leaf sprigs, on a light branch skeleton with textured, normal-mapped bark. Detail comes from textures, not polygon count.

The approved set is 10 trees: maple, cherry, spruce and columnar, each large and young, plus a street tree and a Japanese maple. Their sizes and triangle counts are in `art-source/blender/environment/trees/README.md`.

- **Yard trees** keep today's placement rules in [World & Neighborhood → Yard landscaping](world.md#yard-landscaping): procedural, seeded per lot, clear of the house, walkway, fence and each other. Only the models change.
- **No street trees** ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)). The 0.75 m grass strip stays as it is.
- **What Unity needs** (from the trees README): leaves on a double-sided, alpha-clip material; mipmaps that preserve alpha coverage; imported (not recalculated) normals; and textures shared across trees.

**Open** ([#804](https://github.com/derekwinters/lucas-doggiehood/issues/804), [#811](https://github.com/derekwinters/lucas-doggiehood/issues/811)): which of the 10 trees appear in yards, and how today's large and small yard-tree picks map onto them; whether the street-tree model is used at all; and fading leaf cards that turn edge-on to the camera.

## Landscaping is something dogs ask for

Flower beds, shrubs, hedges and similar yard planting are **not** world dressing placed automatically ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)). Each one is a purchasable item a dog asks for through a quest, installed on its lot the way a [fence](world.md#backyard-fences) is ([#318](https://github.com/derekwinters/lucas-doggiehood/issues/318)).

**Open:** the landscaping items, their prices and tiers, and where each sits on a lot. Derek decides these later with the renders in front of him ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)).

## Street props

Derek approved 26 street props as a reference set ([#821](https://github.com/derekwinters/lucas-doggiehood/issues/821)). Six are **in use** in the game ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)):

| Prop | Model | Placement |
|---|---|---|
| Stop sign | `stop_sign` | One on each side-street approach of every intersection, on the right-hand corner. It is the only sign in the game. |
| Lamp post (lantern) | `lamp_post_lantern` | In the grass strip at every intersection corner, plus one at each property line along the block |
| Mailbox | `mailbox` | One per house, at the curb beside its front walkway |
| Fire hydrant | `fire_hydrant` | One per block, at a corner in the grass strip |
| Park bench | `bench_park` | Only at a few corners (for example one corner per intersection), never in front of a house |
| Newspaper box | `newspaper_box` | Only at a few corners (for example one corner per intersection), never in front of a house |

Sizes and triangle counts are in `art-source/blender/environment/street-props/README.md`.

**Reference only:** every other sign (pedestrian crossing signs don't fit a world with only dogs in it), the cobra-head street light, the bus-stop bench, the trash and recycling carts, the dog-waste station, the bike rack, the utility pole and wires, and the road decals.

**Lamps don't glow.** The lantern's glass shows no light until the game has a night ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)); lighting stays static daytime ([Lighting & time](world.md#lighting-time)). The source model's glass is emissive, so the in-game material turns that off.

**Invariant — a street prop never stands on the roadway and never blocks a sidewalk, walkway or front door** ([#797](https://github.com/derekwinters/lucas-doggiehood/issues/797)).

**Open** ([#805](https://github.com/derekwinters/lucas-doggiehood/issues/805), [#812](https://github.com/derekwinters/lucas-doggiehood/issues/812)): which corners get the bench and newspaper box; exact offsets inside the 0.75 m grass strip; and how the props follow map expansion onto new tiles.

## Fences

The backyard fence keeps the geometry `LotFence` traces today ([Backyard fences](world.md#backyard-fences)), built from modular Blender pieces.

**Open** ([#806](https://github.com/derekwinters/lucas-doggiehood/issues/806), [#813](https://github.com/derekwinters/lucas-doggiehood/issues/813)): the fence style or styles, and how a fence meets a house's upgrade pieces ([#783](https://github.com/derekwinters/lucas-doggiehood/issues/783)).

## Build checklist

- [x] Textured trees committed as source ([#816](https://github.com/derekwinters/lucas-doggiehood/issues/816))
- [x] Street-prop reference set committed as source, with the six in-use props marked ([#821](https://github.com/derekwinters/lucas-doggiehood/issues/821))
- [ ] Lawn texture and lot ground ([#801](https://github.com/derekwinters/lucas-doggiehood/issues/801))
- [ ] Road kit for every road tile type, matching today's widths, offsets and crosswalks ([#802](https://github.com/derekwinters/lucas-doggiehood/issues/802))
- [ ] Sidewalk, front walkway and driveway surfaces ([#803](https://github.com/derekwinters/lucas-doggiehood/issues/803))
- [ ] Trees for yards chosen from the approved set ([#804](https://github.com/derekwinters/lucas-doggiehood/issues/804))
- [ ] The six in-use street props exported for import ([#805](https://github.com/derekwinters/lucas-doggiehood/issues/805))
- [ ] Backyard fence pieces ([#806](https://github.com/derekwinters/lucas-doggiehood/issues/806))
- [ ] Derek approves every world model ([#807](https://github.com/derekwinters/lucas-doggiehood/issues/807))
- [ ] Lots render the textured lawn ([#808](https://github.com/derekwinters/lucas-doggiehood/issues/808))
- [ ] Roads render with the Blender road kit; widths, sidewalk offsets, crosswalks and truck paths unchanged ([#809](https://github.com/derekwinters/lucas-doggiehood/issues/809))
- [ ] Sidewalks, walkways and driveways render with the approved surfaces; a driveway adds no walk-network edge ([#810](https://github.com/derekwinters/lucas-doggiehood/issues/810))
- [ ] Yard trees render the textured models, placement unchanged; no street trees ([#811](https://github.com/derekwinters/lucas-doggiehood/issues/811))
- [ ] The six in-use props are placed by the rules above, lamps unlit ([#812](https://github.com/derekwinters/lucas-doggiehood/issues/812))
- [ ] Fences build from the Blender pieces ([#813](https://github.com/derekwinters/lucas-doggiehood/issues/813))
- [ ] Every invariant on this page has a test that enforces it, including that no reference-only model is under `Assets/`
