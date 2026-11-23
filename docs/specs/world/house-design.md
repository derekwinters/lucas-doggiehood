# House Design

*Epic: [#767](https://github.com/derekwinters/lucas-doggiehood/issues/767). Spec: [#773](https://github.com/derekwinters/lucas-doggiehood/issues/773). Decisions: [#768](https://github.com/derekwinters/lucas-doggiehood/issues/768), [#769](https://github.com/derekwinters/lucas-doggiehood/issues/769), [#770](https://github.com/derekwinters/lucas-doggiehood/issues/770), [#771](https://github.com/derekwinters/lucas-doggiehood/issues/771), [#772](https://github.com/derekwinters/lucas-doggiehood/issues/772), [#789](https://github.com/derekwinters/lucas-doggiehood/issues/789), [#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)*

Houses are our own Blender models. A house is a **base** (the main shell) plus the **upgrade pieces** it has earned, each attached at a named point on the house. A newly built house shows only its **framing**, as if it is still being built, and becomes the **finished** house when a dog moves in. Each upgrade adds the next piece on one fixed path: a **garage**, then a **deck**, then a **patio**.

!!! warning "This is the contract, not yet the code"
    This page describes the house system being built in epic [#767](https://github.com/derekwinters/lucas-doggiehood/issues/767). The game still renders the Kenney City Kit houses, their per-level model swap and the greyscale vacancy tint described in [Art & UI Style → House architecture](art-style.md#house-architecture) and [Neighborhood Expansion](../expansion.md#house-leveling) until the implementation issues land: build state [#777](https://github.com/derekwinters/lucas-doggiehood/issues/777) and [#609](https://github.com/derekwinters/lucas-doggiehood/issues/609), upgrade pieces [#778](https://github.com/derekwinters/lucas-doggiehood/issues/778) and [#783](https://github.com/derekwinters/lucas-doggiehood/issues/783), saves [#779](https://github.com/derekwinters/lucas-doggiehood/issues/779), and the switch-over [#789](https://github.com/derekwinters/lucas-doggiehood/issues/789). No implementation starts before Derek approves every model on [#784](https://github.com/derekwinters/lucas-doggiehood/issues/784).

The approved concept models, colour schemes and the measurements on this page are committed as source under `art-source/blender/` ([#814](https://github.com/derekwinters/lucas-doggiehood/issues/814)); its `README.md` has the full dimensions, triangle counts and concerns.

## How the models are made

Every base and piece is a **Blender Python script** written by Claude and reviewed by Derek ([#768](https://github.com/derekwinters/lucas-doggiehood/issues/768)). How the scripts, exports and renders work is on [Blender Asset Pipeline](../../engineering/blender-pipeline.md). All models are built and approved before any Core or Unity work starts. The pipeline conventions (export format, naming, axis convention, import settings) are **Open** on [#774](https://github.com/derekwinters/lucas-doggiehood/issues/774), [#775](https://github.com/derekwinters/lucas-doggiehood/issues/775) and [#776](https://github.com/derekwinters/lucas-doggiehood/issues/776). Every model stays within the [art budget](art-style.md#art-budget).

Model conventions the concepts already follow:

- 1 Blender unit is 1 m at in-game size. No kit scale factor is applied.
- A base's **pivot** is the centre of its wall footprint, on the ground.
- The **front door faces the street** (−Y in Blender). The piece side is +X and the back is +Y.
- Slab top 0.35 m, finished floor 0.65 m, walls 0.22 m thick, on every base.

## Anatomy: a base plus pieces at attach points

A house is one **base** plus zero to three **upgrade pieces**. Each piece sits at an **attach point**: a named position on the house where that piece's origin goes.

| Attach point | Declared by | Where | Piece |
|---|---|---|---|
| Side | every base | on the ground at the piece-side wall (in the concepts, the addition's centre sits 3.75 m in front of the back wall) | Garage (level 2) |
| Back | every base | on the ground at the back wall's outer face, centred on the back door | Deck (level 3) |
| Patio | the garage piece | the back edge of the garage's slab, centred on it | Patio (level 4) |

The concept scripts name these `ATTACH_ADDITION`, `ATTACH_DECK` and `ATTACH_PATIO`; each base's coordinates are in `art-source/blender/README.md`. The final names in Core are **Open** ([#778](https://github.com/derekwinters/lucas-doggiehood/issues/778)).

**Room for later.** Toys, pools and real dog models will reuse the same Blender pipeline and conventions in their own milestones. This page does not specify any of them.

## The 10 bases

Ten bases replace the Kenney houses everywhere ([#769](https://github.com/derekwinters/lucas-doggiehood/issues/769), raised from 5 to 10). Every base has a **framing** and a **finished** version built from the same dimensions.

| # | Base | Style | Wall footprint (m) | Front line (`FRONT_Y`) |
|---|---|---|---|---|
| 1 | Side-gable cottage | Small side-gabled cottage, shuttered windows, outside chimney | 7.5 × 7.0 | Facade, −3.5 |
| 2 | Ranch | Long, low, hipped | 11.4 × 7.2 | Facade, −3.6 |
| 3 | Two-storey colonial | Tall symmetrical box, full-width porch | 8.4 × 7.0, plus a 1.8 m porch | **Porch front**, −5.3 |
| 4 | Craftsman bungalow | Front gable over an inset porch | 8.6 × 10.0 (includes a 2.2 m inset porch) | **Porch front**, −5.0 |
| 5 | Dutch colonial | Gambrel roof with a front shed dormer | 8.4 × 8.0 | Facade, −4.0 |
| 6 | Split-level | Tall front-gabled wing beside a low side-gabled wing | 11.0 × 7.8 | Tall wing's front wall, −3.9 |
| 7 | Modern farmhouse | Steep front gable over a wrap porch | 7.2 × 9.0, plus a 2.0 m wrap porch | **Porch front**, −6.5 |
| 8 | Storybook Tudor | Steep side gable, steeper front cross-gable | 9.6 × 7.6, plus a cross-gable 1.4 m forward | **Cross-gable front**, −5.2 |
| 9 | Spanish mission | Low hip, arched loggia, curved parapet | 10.4 × 7.4, plus a 4.8 × 2.0 m loggia | **Loggia / courtyard wall front**, −5.7 |
| 10 | Queen Anne | Two storeys, steep front gable, corner turret | 8.0 × 8.6, plus a turret and a 2.0 m porch | **Porch front**, −6.3 |

**The ranch changes ([#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)).** The concept ranch has a built-in garage. The base ranch loses it, and its garage arrives as the level-2 piece like on every other base. The rebuilt ranch is in `art-source/blender/` as a reference asset ([#785](https://github.com/derekwinters/lucas-doggiehood/issues/785)). It keeps its 11.4 × 7.2 m footprint: the old garage bay is living space with two front windows, so its front line and attach points are unchanged.

### Framing and finished versions

- **Finished:** the complete house: siding, roof covering, trim, windows, doors, chimney and porch.
- **Framing:** the house mid-build: slab, floor framing, stud walls with plates and headers, gable studs, rafters and ridge board, and a few temporary braces. No siding, roof covering, trim or chimney.

The slab, porch decks, steps and footings are identical in both versions, so the two line up exactly. Only the roof overhangs and exterior trim differ.

## Build states

A house is in one of two build states:

- **Framing** while the house is **vacant**.
- **Finished** once a dog **moves in**.

Vacancy (`House.IsVacant`) stays the single source; the build state adds no new timers or saved state ([#777](https://github.com/derekwinters/lucas-doggiehood/issues/777)). Move-ins are permanent, so a house never goes back to framing.

**Pieces appear finished right away** ([#772](https://github.com/derekwinters/lucas-doggiehood/issues/772)). Only the base shows framing, and each piece needs only a finished model. The house profile offers no Upgrade for a vacant house ([House Profile](../ui/house-profile.md)), so in practice a framing house has no pieces yet.

**Invariant — a vacant house always renders its framing version, never the finished shell or a tint.** This replaces the greyscale vacancy tint ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58)).

**Invariant — a house's appearance is a pure function of its Core state.** Its base, colour scheme, installed pieces and occupancy decide what it looks like. The view draws what Core resolves and never decides on its own.

**Invariant — framing and finished versions share one footprint and pivot.** Swapping one for the other never moves the house or changes where its pieces attach.

## Upgrade pieces: the fixed path

Every house follows one fixed path; the player never picks a piece ([#770](https://github.com/derekwinters/lucas-doggiehood/issues/770), [#771](https://github.com/derekwinters/lucas-doggiehood/issues/771), changed by [#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)):

| Level | Piece added | Upgrade cost | Decoration slots |
|---|---|---|---|
| 1 | none (the base) | built for the [house build price](../expansion.md#pricing) | 1 |
| 2 | **Garage** | 100 | 2 |
| 3 | **Deck** | 200 | 3 |
| 4 | **Patio** | 400 | 4 |

Costs and slots are unchanged from [House leveling](../expansion.md#house-leveling). A house at level N has the first N−1 pieces of the path. The existing Upgrade button on the [house profile](../ui/house-profile.md) installs the next piece, so no new screen is needed.

### Garage (level 2)

Decided on [#817](https://github.com/derekwinters/lucas-doggiehood/issues/817):

- **Attached** to the house, on the piece side (where the concept addition went).
- **One car.** The garage ships on its own; cars are a separate, later feature that will build on it.
- **The door faces the street**, with a **driveway** from the curb across the front yard and sidewalk.
- The patio sits **behind** the garage.
- The driveway is **ground decoration only**, in front of the garage. It adds no walk-network edge; dogs still reach the front door by the front walkway ([#128](https://github.com/derekwinters/lucas-doggiehood/issues/128)). See [World Art](world-art.md#sidewalks-front-walkways-and-driveways).

**Built:** the garage model is in `art-source/blender/` as a reference asset (`piece_garage.py`, exported as `piece-garage.glb` and as level 2–4 of every base; [#780](https://github.com/derekwinters/lucas-doggiehood/issues/780), [#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)). It is 3.5 m out × 6.0 m along the wall, with a 2.6 × 2.3 m sectional door, a short sloped apron and a back door onto the patio. Its roof keeps each base's tie-in, with its pitch lowered so its ridge matches the concept addition's. It attaches at the existing side attach point, so it sits 0.25–4.25 m behind the front line rather than flush with it.

**Open:** the exact driveway geometry ([#803](https://github.com/derekwinters/lucas-doggiehood/issues/803)). The reference assets carry a proposal as a separate object (`driveway-base-NN.glb`): a plain 3.0 m concrete slab from the garage apron to the curb that skips the sidewalk.

### Deck (level 3)

A raised wooden deck at the back door: 6.0 × 4.0 m on every base, surface 0.60 m (just under the back door), railing in the trim colour. Its stairs leave the deck's piece-side edge and **land on the patio**. Each base solves the stair position so they do.

### Patio (level 4)

Pavers 8 cm tall, directly behind the garage at the garage's patio attach point. It takes the garage's width and keeps its 5.5 m depth: **3.5 × 5.5 m** ([#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)).

> **How the spec is changing ([#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)).** This page used to list the garage model, the ranch rebuild and the patio's size with the garage as Open, and its clearances used the concept addition → it now says the garage and the garage-less ranch are built as reference assets, the patio is as wide as the garage (3.5 × 5.5 m), and the clearances below are measured with the garage → because Derek approved the garage concept as shown.

**Invariant — a piece attaches only at an attach point its house declares.** The garage and deck use points their base declares; the patio uses the point the garage declares. A base that does not declare a point cannot take that piece.

**Invariant — an upgrade piece never extends outside its lot's envelope, and never covers the front walkway, the sidewalk or a neighbour's lot.** The garage's driveway is the one planned crossing of the sidewalk, and its geometry is Open above.

> **How the spec is changing ([#800](https://github.com/derekwinters/lucas-doggiehood/issues/800)).** This page used to leave open how the garage's driveway joins the walk network → it now says the driveway is ground decoration only and adds no walk-network edge → because Derek confirmed that dogs keep reaching the door by the front walkway.

## Colour schemes

Every house is painted in one of **12 realistic colour schemes** ([#814](https://github.com/derekwinters/lucas-doggiehood/issues/814)), defined once as data in `art-source/blender/colour-schemes.json`. A base and its pieces paint only through the scheme's slots, so pieces always match their house.

| Slot | Paints |
|---|---|
| `siding` | Main wall colour |
| `roof`, `roof_style` | Roof colour, and `shingle`, `tile` or `metal` |
| `trim` | Window and door frames, fascia, corner boards, deck railing |
| `shutter` | Window shutters |
| `door` | Front and back doors |
| `chimney` | Chimney masonry |
| `flowerbox`, `flower_a`, `flower_b` | Window boxes and their two flower colours |
| `deck` | Deck boards and stairs |
| `patio_a`, `patio_b` | The two paver tones |

The 12 schemes are Colonial White, Cream & Forest, Adobe Tile, Greige Modern, Dove Gray, Charcoal Craftsman, Coastal Blue, Navy Saltbox, Sage Cottage, Red Farmhouse, Buttercream and Cedar Lodge. Their hex values are in the JSON file.

## Which base and scheme a house gets

Decided on [#789](https://github.com/derekwinters/lucas-doggiehood/issues/789):

- **The four starter houses** stay on **bases 1–4**.
- **Zone-built houses** roll from **all 10 bases** plus a **random colour scheme**. The pick is made once and kept through upgrades.
- **Existing saves:** each of today's 5 Kenney ladders maps to one base (ladder 1 → base 1 … ladder 5 → base 5), and a level-N house gets the first N−1 pieces.

**Open:**

- Which scheme each starter house uses ([#789](https://github.com/derekwinters/lucas-doggiehood/issues/789)).
- When a zone house's roll happens. Today's rule rolls when the zone unlocks ([#434](https://github.com/derekwinters/lucas-doggiehood/issues/434)), so the empty lot already knows its house; #789 says "chosen once at build".
- Which side fence the piece side faces on a given lot. Every base fits with its pieces on either side ([#773](https://github.com/derekwinters/lucas-doggiehood/issues/773)).

## Placement on the lot

**The lot positions the level-4 house** ([#773](https://github.com/derekwinters/lucas-doggiehood/issues/773)). The house is placed by its **level-4 footprint**: the base plus every piece on the path, including roof overhangs, chimneys and steps.

- **Across the lot:** that footprint is centred between the two side runs of the backyard fence.
- **Front to back:** the base's **front line** sits at the front setback.
- **Nothing moves when a piece is added.** This replaces today's fixed lot anchor (`NeighborhoodLayout.LotDistanceFromCenter` = 14 m) and the per-level re-alignment of [#454](https://github.com/derekwinters/lucas-doggiehood/issues/454).

**Invariant — a fully upgraded house always fits its lot.** Placement uses the level-4 footprint, so the house fits before any piece is bought and no piece can push it out later.

### Front line and setback

Each base declares its **front line** (`FRONT_Y` in the table above): the facade on most bases, and the porch, cross-gable or loggia front where one stands forward. The front line, not the main wall, sits at the setback, so a porch never pokes into the front yard.

**The front setback grows by 1.0 m, from 2.75 m to 3.75 m from the sidewalk's outer edge** (9.5 m from the street centreline), to make room for front landscaping. Derek asked to push houses back "slightly" ([#773](https://github.com/derekwinters/lucas-doggiehood/issues/773)); 1.0 m is this spec's proposal for that.

### Clearances on the tightest starter lot

All 4 starting lots are corner lots with the same envelope. These are the clearances for each base at level 4 with the new 3.75 m setback, measured with `level4_placement()` in `art-source/blender/dh_scene.py`. They come out the same with the pieces on the road side or the neighbour side.

| Base | Level-4 width × depth (m) | Each side fence | Front → sidewalk | Rear → fence |
|---|---|---|---|---|
| 1 Cottage | 12.39 × 13.50 | 3.93 | 2.15 | 6.60 |
| 2 Ranch | 15.92 × 13.50 | **2.17** | 2.35 | 6.40 |
| 3 Colonial | 13.29 × 14.34 | 3.48 | 3.11 | 4.80 |
| 4 Craftsman | 13.49 × 15.54 | 3.38 | 3.11 | 3.60 |
| 5 Dutch colonial | 13.29 × 14.30 | 3.48 | 2.35 | 5.60 |
| 6 Split-level | 15.39 × 13.09 | 2.43 | 3.36 | 5.80 |
| 7 Farmhouse | 13.43 × 16.54 | 3.41 | 3.11 | **2.60** |
| 8 Tudor | 14.49 × 15.20 | 2.88 | 2.45 | 4.60 |
| 9 Mission | 14.86 × 14.70 | 2.69 | 3.35 | 4.20 |
| 10 Queen Anne | 13.40 × 16.14 | 3.43 | 3.11 | 3.00 |

"Front → sidewalk" is measured from the front-most thing (steps, stoop or porch front); a base's front line itself is 3.75 m from the sidewalk. These numbers use the **garage** as the level-2 piece ([#817](https://github.com/derekwinters/lucas-doggiehood/issues/817)). Every base still fits. The tightest side clearance is the ranch at 2.17 m, and the tightest rear clearance is the farmhouse at 2.60 m. Compared with the concept addition, every base gains 0.25 m per side (the garage is 0.5 m narrower) and loses 0.75 m at the rear (the garage is 1.5 m deeper, so the patio behind it starts further back). Zone and cul-de-sac lots have not been checked yet (**Open**, [#778](https://github.com/derekwinters/lucas-doggiehood/issues/778)).

## Open items

Besides those marked above:

- **Footprint definition.** Placement, walkway and fence code need a declared footprint per base (the slab or wall rectangle) rather than measured mesh bounds, because framing and finished meshes have different bounds ([#777](https://github.com/derekwinters/lucas-doggiehood/issues/777), [#778](https://github.com/derekwinters/lucas-doggiehood/issues/778)).
- **Fence connectors, walkway and yard trees** around the bigger house, including where the side-fence connector meets a garage or the farmhouse's wrap porch ([#783](https://github.com/derekwinters/lucas-doggiehood/issues/783)).
- **Applying schemes in Unity**, by material slot or by export per scheme, and the roof textures ([#774](https://github.com/derekwinters/lucas-doggiehood/issues/774)).
- **Axis convention** between Blender and the game's model yaw ([#774](https://github.com/derekwinters/lucas-doggiehood/issues/774)).

## Build checklist

- [x] Concept models for all 10 bases and the 3 pieces, and the 12 colour schemes, committed as source ([#814](https://github.com/derekwinters/lucas-doggiehood/issues/814))
- [ ] Blender pipeline conventions page and one-command export ([#774](https://github.com/derekwinters/lucas-doggiehood/issues/774), [#775](https://github.com/derekwinters/lucas-doggiehood/issues/775))
- [ ] Garage model replaces the addition ([#780](https://github.com/derekwinters/lucas-doggiehood/issues/780)), and the ranch's built-in garage moves to its level-2 piece ([#785](https://github.com/derekwinters/lucas-doggiehood/issues/785))
- [ ] Derek approves every house model ([#784](https://github.com/derekwinters/lucas-doggiehood/issues/784))
- [ ] Unity import settings and load checks for house parts ([#776](https://github.com/derekwinters/lucas-doggiehood/issues/776))
- [ ] Core resolves each house's appearance (base, scheme, build state, pieces); a vacant house resolves to framing, an occupied one to finished ([#777](https://github.com/derekwinters/lucas-doggiehood/issues/777))
- [ ] A vacant house renders its own framing version, not a tint ([#609](https://github.com/derekwinters/lucas-doggiehood/issues/609))
- [ ] Each upgrade installs the next piece on the fixed path (garage, deck, patio) at its attach point; costs and slots unchanged ([#778](https://github.com/derekwinters/lucas-doggiehood/issues/778))
- [ ] Houses are placed by their level-4 footprint, centred between the side fences, front line at the 3.75 m setback; nothing moves on upgrade ([#778](https://github.com/derekwinters/lucas-doggiehood/issues/778), [#789](https://github.com/derekwinters/lucas-doggiehood/issues/789))
- [ ] Installed pieces draw in the world and the house-profile thumbnail, with fences, walkway and trees fitting around them ([#783](https://github.com/derekwinters/lucas-doggiehood/issues/783))
- [ ] Bases, schemes and installed pieces save and load ([#779](https://github.com/derekwinters/lucas-doggiehood/issues/779))
- [ ] Starters on bases 1–4, zone houses rolled from all 10 bases plus a scheme, existing saves mapped, Kenney houses retired ([#789](https://github.com/derekwinters/lucas-doggiehood/issues/789))
- [ ] Every invariant on this page has a test that enforces it
