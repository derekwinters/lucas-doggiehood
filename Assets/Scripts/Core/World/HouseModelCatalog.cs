using System;
using System.Collections.Generic;
using Doggiehood.Core.Art;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The house-model catalog (#125): every City Kit Suburban model the
    /// game can place, with its authored footprint and front-door data
    /// (<see cref="HouseModel"/>). Adding a house model is one FBX + one
    /// catalog row here; the completeness test
    /// (HouseModelCatalogTests.ForHouse_HasACatalogEntryForEveryHouseLot)
    /// makes forgetting the row impossible.
    ///
    /// The houseId -> model assignment itself lives on
    /// <see cref="HouseStyleTable"/> (#64), alongside which kit texture
    /// variant tints that house — model + tint are one styling decision
    /// with one source of truth. <see cref="ForHouse"/> here just resolves
    /// that assignment down to the geometry entry.
    ///
    /// Footprints are parsed from the kit GLB geometry (model-local units).
    /// Door points are AUTHORED DATA from Derek's #126 gallery review,
    /// pass 1 (2026-07-14): he moved each gallery DoorMarker sphere onto
    /// the visible door of the rendered mesh and read the Inspector local
    /// position in the entry container (gallery yaw is 0, so container
    /// axes == model axes); dividing by the entry's then-current uniform
    /// scale (8 / the model's max footprint — the pre-#145 normalization
    /// rule) gives these model-local values, rounded to 4 decimals. The
    /// doors are recessed behind the front facade (porches;
    /// building-type-b's is near its footprint center) — that observation
    /// is what turned the door datum from a facade scalar into a 2D point.
    /// The later #59 Level-1 starter meshes (r/h/q) instead carry
    /// PROVISIONAL PLACEHOLDER door points (centered on X, toward the
    /// street) pending their own gallery authoring pass — see their inline
    /// note below.
    /// </summary>
    public static class HouseModelCatalog
    {
        public static IReadOnlyList<HouseModel> Models { get; } = new[]
        {
            new HouseModel("building-type-b", 1.828f, 1.140f, -0.2612f, 0.0446f),
            new HouseModel("building-type-g", 1.450f, 1.178f, 0.0769f, -0.3382f),
            new HouseModel("building-type-k", 0.921f, 1.020f, 0.1900f, -0.3672f),
            new HouseModel("building-type-m", 1.428f, 1.428f, -0.0464f, -0.6105f),

            // Level-1 (as-built) starter meshes for the #59 upgrade paths,
            // chosen by Derek (2026-07-25) to replace the #122 placeholder
            // starters r/h/q for houses 1/2/4 (house 3 keeps building-type-k
            // above). Footprints are measured from the kit FBX bounding-box
            // extent / 100 — the same model-local convention as b/g/k/m.
            //
            // Door points are PROVISIONAL PLACEHOLDERS, not authored data:
            // centered on X, a quarter of the footprint toward the street
            // (z = -FootprintZ/4). They sit strictly inside the footprint so
            // the within-footprint guardrail holds, and await a Derek
            // gallery authoring pass (the mechanism that produced b/g/k/m's
            // #126 pass-1 measurements) to replace them with the real door
            // anchors of each mesh.
            new HouseModel("building-type-r", 1.028f, 1.020f, 0f, -0.2550f),
            new HouseModel("building-type-h", 1.300f, 0.916f, 0f, -0.2290f),
            new HouseModel("building-type-q", 1.240f, 0.8856f, 0f, -0.2214f),

            // Level-2..4 upgrade meshes for the #59 ladders (v0.6 de-graybox
            // pass, Derek 2026-07-25: "I want the house models we chose at all
            // levels, not graybox"). Every level of every starter house now
            // resolves to a real catalog entry, so a leveled-up home renders
            // its chosen kit mesh — visibly growing — instead of the graybox
            // placeholder. Footprints are measured from each kit FBX
            // bounding-box extent / 100 (the b/g/k/m and r/h/q convention).
            // Ladders: house1 r->c->s->b, house2 h->i->g->f,
            // house3 k->l->j->d, house4 q->e->u->n.
            //
            // Door points are PROVISIONAL PLACEHOLDERS, not authored data:
            // centered on X, a quarter of the footprint toward the street
            // (z = -FootprintZ/4) — the same provisional pattern as the r/h/q
            // L1 entries. They sit strictly inside the footprint so the
            // within-footprint guardrail holds, and await a Derek gallery
            // authoring pass (the mechanism that produced b/g/k/m's #126
            // pass-1 measurements) to replace them with each mesh's real door
            // anchor. The models themselves are final; only the door anchors
            // are deferred.
            new HouseModel("building-type-c", 1.2864f, 1.0281f, 0f, -0.2570f),
            new HouseModel("building-type-s", 1.4060f, 1.0864f, 0f, -0.2716f),
            new HouseModel("building-type-f", 1.4280f, 1.4059f, 0f, -0.3515f),
            new HouseModel("building-type-i", 1.2864f, 1.0280f, 0f, -0.2570f),
            new HouseModel("building-type-l", 1.0336f, 1.0200f, 0f, -0.2550f),
            new HouseModel("building-type-j", 1.3700f, 0.9160f, 0f, -0.2290f),
            new HouseModel("building-type-d", 1.7564f, 1.0280f, 0f, -0.2570f),
            new HouseModel("building-type-e", 1.3000f, 1.0280f, 0f, -0.2570f),
            new HouseModel("building-type-u", 1.4280f, 1.0869f, 0f, -0.2717f),
            new HouseModel("building-type-n", 1.7843f, 1.3779f, 0f, -0.3445f),

            // 5th-house-ladder meshes staged for the #299 houses art pass
            // (#348 asset step; building-type-m above already carries its
            // own row). Footprints measured from each kit FBX bounding-box
            // extent / 100 — the same model-local convention b/g/k/m, the
            // r/h/q L1 starters, and the L2-L4 ladder meshes use.
            //
            // Door points are PROVISIONAL PLACEHOLDERS, not authored data:
            // centered on X, a quarter of the footprint toward the street
            // (z = -FootprintZ/4) — the same provisional pattern as the r/h/q
            // and L2-L4 entries. They sit strictly inside the footprint so
            // the within-footprint guardrail holds, and await a Derek gallery
            // authoring pass to replace them with each mesh's real door anchor.
            new HouseModel("building-type-o", 1.2700f, 1.0280f, 0f, -0.2570f),
            new HouseModel("building-type-p", 1.2400f, 0.9900f, 0f, -0.2475f),
            new HouseModel("building-type-a", 1.3000f, 1.0281f, 0f, -0.2570f),
        };

        public static HouseModel ForModel(string modelName)
        {
            foreach (var model in Models)
            {
                if (model.ModelName == modelName)
                {
                    return model;
                }
            }

            throw new ArgumentException($"No catalog entry for model '{modelName}'.", nameof(modelName));
        }

        /// <summary>
        /// Non-throwing check for whether <paramref name="modelName"/> has a
        /// catalog entry (#59). Every mesh named by the #59 level-swap ladders
        /// (<see cref="HouseLevelModelTable"/>) now has one (v0.6 de-graybox
        /// pass — all four levels of every starter house render their chosen
        /// kit mesh), so the Unity layer only steers to the graybox fallback
        /// for a genuinely-unknown mesh — e.g. an expansion-built house with
        /// no ladder — instead of catching <see cref="ForModel"/>'s
        /// ArgumentException.
        /// </summary>
        public static bool HasModel(string modelName)
        {
            foreach (var model in Models)
            {
                if (model.ModelName == modelName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The fixed level at which a zone house's rolled ladder is
        /// resolved for placement/footprint purposes (#414). The placement and
        /// footprint call sites carry only a <c>HouseLot</c> (no
        /// <c>house.Level</c>), and leveling a zone house never resizes or
        /// moves its lot — every ladder mesh fits the shared lot envelope — so
        /// the level-1 mesh is a valid, level-invariant footprint input.
        /// (Rendering the correct visual level is unaffected:
        /// WorldBuilder.BuildHouse resolves the mesh via <c>house.Level</c>.)</summary>
        private const int ZoneHousePlacementLevel = HouseLevelModelTable.MinLevel;

        /// <summary>
        /// House id -> model. Starter ids (1-4) resolve via
        /// Doggiehood.Core.Art.HouseStyleTable (#64) — the single source of
        /// truth for their model + tint. Zone-built ids (>= 5) have no
        /// HouseStyleTable style, so they resolve through the #299 rolled
        /// ladder instead (#414): HouseVariantAssignment picks the ladder,
        /// HouseLevelModelTable names its level-1 mesh, and ForModel returns
        /// the catalog entry. This one chokepoint keeps every placement,
        /// footprint, and fence call site zone-safe without each branching on
        /// the id itself — a zone lot with a front walkway (once WalkNetwork
        /// spans zone tiles, #398) no longer throws through
        /// HouseStyleTable.ForHouse.
        /// </summary>
        public static HouseModel ForHouse(int houseId)
        {
            if (HouseVariantAssignment.IsZoneHouse(houseId))
            {
                var ladderId = HouseVariantAssignment.ForHouse(houseId).LadderId;
                var meshName = HouseLevelModelTable.ForHouseLevel(ladderId, ZoneHousePlacementLevel);
                return ForModel(meshName);
            }

            return ForModel(HouseStyleTable.ForHouse(houseId).ModelName);
        }
    }
}
