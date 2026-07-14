using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The house-model catalog (#125): every City Kit Suburban model the
    /// game can place, with its authored footprint and front-door data
    /// (<see cref="HouseModel"/>), plus the houseId -> model assignment
    /// that used to live in WorldBuilder.HouseModels. Adding a house model
    /// is one FBX + one catalog row here; the completeness test
    /// (HouseModelCatalogTests.ForHouse_HasACatalogEntryForEveryHouseLot)
    /// makes forgetting the row impossible.
    ///
    /// Footprints are parsed from the kit GLB geometry (model-local units).
    /// Front-door offsets are a FIRST PASS: the fused meshes expose no door
    /// node, so every door is recorded horizontally centered on the front
    /// facade (offset 0) until the #126 debug gallery review with Derek
    /// refines them.
    /// </summary>
    public static class HouseModelCatalog
    {
        public static IReadOnlyList<HouseModel> Models { get; } = new[]
        {
            new HouseModel("building-type-b", 1.828f, 1.140f, 0f, -0.570f),
            new HouseModel("building-type-g", 1.450f, 1.178f, 0f, -0.589f),
            new HouseModel("building-type-k", 0.921f, 1.020f, 0f, -0.510f),
            new HouseModel("building-type-m", 1.428f, 1.428f, 0f, -0.714f),
        };

        /// <summary>
        /// House id -> model assignment, moved from WorldBuilder (#122's
        /// PLACEHOLDER PICKS — Derek and Lucas re-pick the letters once
        /// they have seen the models rendered in the Editor).
        /// </summary>
        private static readonly (int HouseId, string ModelName)[] Assignments =
        {
            (1, "building-type-b"),
            (2, "building-type-g"),
            (3, "building-type-k"),
            (4, "building-type-m"),
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

        public static HouseModel ForHouse(int houseId)
        {
            foreach (var assignment in Assignments)
            {
                if (assignment.HouseId == houseId)
                {
                    return ForModel(assignment.ModelName);
                }
            }

            throw new ArgumentException($"No house model mapped for id {houseId}.", nameof(houseId));
        }
    }
}
