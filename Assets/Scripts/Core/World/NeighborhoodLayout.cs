using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The starting neighborhood (#7, #38): two streets crossing at the
    /// origin, one house lot per quadrant. Distances are meters on the
    /// ground plane; the Unity layer builds world geometry from this data.
    /// </summary>
    public static class NeighborhoodLayout
    {
        /// <summary>
        /// Same concept as <see cref="WorldDimensions.RoadWidth"/> (#105) —
        /// referencing it here keeps the 6m value defined in exactly one
        /// place while preserving this map's own public API/name, which
        /// other Core and Unity code already depends on.
        /// </summary>
        public const float StreetWidth = WorldDimensions.RoadWidth;

        /// <summary>
        /// This map's own lot-placement choice, not one of the locked
        /// standard dimensions (#105) — it's specific to this starting
        /// neighborhood instance, not a road/sidewalk/crosswalk measurement,
        /// so it intentionally stays a standalone constant rather than
        /// being derived from <see cref="WorldDimensions"/>.
        /// </summary>
        public const float LotDistanceFromCenter = 14f;

        /// <summary>
        /// How far each street extends from the intersection (#106). Like
        /// <see cref="LotDistanceFromCenter"/>, this is a placement choice
        /// specific to this starting map instance, not one of the locked
        /// #105 standard dimensions.
        /// </summary>
        public const float StreetHalfLength = 26f;

        public static readonly GridPoint Intersection = new GridPoint(0f, 0f);

        public static IReadOnlyList<Street> Streets { get; } = new[]
        {
            new Street(StreetOrientation.NorthSouth),
            new Street(StreetOrientation.EastWest),
        };

        /// <summary>
        /// Real Road geometry (#106) built from <see cref="Streets"/>: one
        /// Road per Street, centered on <see cref="Intersection"/>.
        /// </summary>
        public static IReadOnlyList<Road> Roads { get; } = Streets
            .Select(street => new Road(street.Orientation, Intersection, StreetHalfLength))
            .ToList();

        public static IReadOnlyList<HouseLot> HouseLots { get; } = new[]
        {
            new HouseLot(1, Quadrant.NorthEast, new GridPoint(LotDistanceFromCenter, LotDistanceFromCenter)),
            new HouseLot(2, Quadrant.NorthWest, new GridPoint(-LotDistanceFromCenter, LotDistanceFromCenter)),
            new HouseLot(3, Quadrant.SouthEast, new GridPoint(LotDistanceFromCenter, -LotDistanceFromCenter)),
            new HouseLot(4, Quadrant.SouthWest, new GridPoint(-LotDistanceFromCenter, -LotDistanceFromCenter)),
        };

        private static WalkNetwork walkNetwork;

        /// <summary>
        /// The cached sidewalk+crosswalk+front-walkway network (#106, #128) for
        /// this starting neighborhood — built once from <see cref="Roads"/>
        /// and <see cref="HouseLots"/>.
        /// </summary>
        public static WalkNetwork WalkNetwork
        {
            get { return walkNetwork ?? (walkNetwork = WalkNetwork.BuildFrom(Roads, HouseLots)); }
        }

        public static HouseLot GetHouseLot(int houseId)
        {
            foreach (var lot in HouseLots)
            {
                if (lot.HouseId == houseId)
                {
                    return lot;
                }
            }

            throw new ArgumentException($"No house lot with id {houseId}.", nameof(houseId));
        }
    }
}
