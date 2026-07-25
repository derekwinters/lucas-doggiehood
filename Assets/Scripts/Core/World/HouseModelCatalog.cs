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
        /// House id -> model, via Doggiehood.Core.Art.HouseStyleTable
        /// (#64): the houseId -> model assignment that used to live here
        /// as a separate hardcoded list moved to HouseStyleTable so
        /// model + tint selection has one source of truth instead of two
        /// disconnected tables. HouseStyleTable.ForHouse throws the same
        /// ArgumentException shape for an unknown id, so this delegation
        /// changes no caller-visible behavior.
        /// </summary>
        public static HouseModel ForHouse(int houseId)
        {
            return ForModel(HouseStyleTable.ForHouse(houseId).ModelName);
        }
    }
}
