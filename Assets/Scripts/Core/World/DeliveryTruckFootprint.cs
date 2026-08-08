namespace Doggiehood.Core.World
{
    /// <summary>
    /// #660: the delivery truck's physical footprint as it relates to crosswalk
    /// right-of-way — how long the body is, how far its bumper and tail reach
    /// past its pivot, and the hard geometric budget those two setbacks must
    /// fit inside.
    ///
    /// This lives in Core rather than on the Unity <c>DeliveryTruckView</c>
    /// because it is a RULE, not a rendering detail: the analysis on #658 proved
    /// that a truck longer than the clear gap between an intersection's two
    /// crosswalk bands necessarily holds BOTH bands at once, so two oncoming
    /// trucks can acquire them in opposite order and wedge permanently — a
    /// lock-ordering cycle that also freezes any dog waiting to cross. The
    /// property used to live only in a comment, so a later scale tweak could
    /// silently reintroduce the deadlock; here it is derived from the locked
    /// <see cref="WorldDimensions"/> constants and pinned by Core tests, so a
    /// road-layout or truck-scale change fails CI instead of freezing the game.
    ///
    /// The view still MEASURES its real body at spawn (renderer bounds of the
    /// body it actually got) and derives its setbacks through this type; the
    /// nominal figures here are what the constraint is checked against, and an
    /// EditMode test ties the measurement back to them.
    /// </summary>
    public static class DeliveryTruckFootprint
    {
        /// <summary>
        /// Length of the staged Kenney Car Kit "delivery" model along its own
        /// forward axis, as imported and BEFORE <see cref="ModelScale"/>.
        /// Measured from the mesh the same documented way
        /// <see cref="WorldDimensions.SidewalkSurfaceHeight"/> was measured from
        /// road-straight.fbx: delivery.fbx's body vertices span Z = -162.5..162.5
        /// in FBX internal units (the file's own UnitScaleFactor is 1, i.e. cm),
        /// so 325 raw units become 3.25m on Unity's default cm-to-m FBX import.
        /// </summary>
        public const float ModelLengthAtImportScale = 3.25f;

        /// <summary>
        /// Width of the same staged "delivery" model across its own lateral axis,
        /// as imported and BEFORE <see cref="ModelScale"/>. Measured from the mesh
        /// the same documented way <see cref="ModelLengthAtImportScale"/> was:
        /// delivery.fbx's body vertices span X = -75..75 in FBX internal units
        /// (the file's own UnitScaleFactor is 1, i.e. cm), so 150 raw units become
        /// 1.5m on Unity's default cm-to-m FBX import. The body is the widest
        /// part — the four wheel meshes reach X = +/-60 once their model
        /// translations are applied, and the door mesh X = +/-55, both inside the
        /// body's own span — so this figure bounds the whole model.
        ///
        /// #672 is what makes the width a budgeted dimension rather than a
        /// rendering detail: once the truck sits a lane offset off the centerline,
        /// its outer side is what decides whether "keep right" put a bumper over
        /// the curb. See <see cref="FitsInsideItsLane"/>.
        /// </summary>
        public const float ModelWidthAtImportScale = 1.5f;

        /// <summary>
        /// Uniform scale the kit model is instantiated at (#547). Bounded above
        /// by <see cref="MaxBodyLength"/>: the truck has to fit between an
        /// intersection's crosswalk bands, so this is not a free visual dial.
        /// The previous value of 3 put the body at 9.75m — half again longer
        /// than the 6.5m gap — which is what deadlocked #658. At 1.5 the body is
        /// 4.875m, so the two setbacks total 5.375m against the 6.5m gap: 1.125m
        /// of margin — the same headroom the front setback had on its own. (1.75
        /// also satisfies the bound but leaves only 0.31m — too little to absorb
        /// any later road-geometry change.) #547 already flags this figure as
        /// needing an on-device look, so it was always provisional.
        /// </summary>
        public const float ModelScale = 1.5f;

        /// <summary>
        /// #639: the daylight left between the truck's front bumper and a
        /// crosswalk's near edge when it yields, so it reads as waiting BEHIND
        /// the stripes rather than nosing onto them.
        /// </summary>
        public const float CrosswalkStopGap = 0.5f;

        /// <summary>
        /// Distance between an intersection's two opposing crosswalk bands,
        /// centre to centre: each sits one crossing-road sidewalk-centre offset
        /// out from the intersection (<see cref="TileCrosswalkGeometry.CrosswalkOffset"/>,
        /// the same place <see cref="WalkNetwork"/> puts its crosswalk edges),
        /// one on either side. Kept as this type's own name for the figure, but
        /// derived from the one road-geometry declaration of it (#673 needs the
        /// same spacing to decide which bands share an intersection).
        /// </summary>
        public const float CrosswalkSpacing = TileCrosswalkGeometry.BandSpacing;

        /// <summary>
        /// The clear roadway between an intersection's two crosswalk bands: the
        /// spacing less one band's own along-road width. A vehicle whose whole
        /// crosswalk footprint fits in here can be clear of one band before it
        /// reaches the next, so it never holds both at once.
        /// </summary>
        public const float ClearGapBetweenCrosswalkBands =
            CrosswalkSpacing - WorldDimensions.CrosswalkWidth;

        /// <summary>The longest body that satisfies
        /// <see cref="FitsBetweenCrosswalkBands"/>, from substituting the two
        /// setbacks below into that inequality: <c>(L/2 + gap) + L/2 &lt; gapBetweenBands</c>
        /// reduces to <c>L &lt; gapBetweenBands - stopGap</c>.</summary>
        public const float MaxBodyLength = ClearGapBetweenCrosswalkBands - CrosswalkStopGap;

        /// <summary>The truck body's length along its travel axis at the
        /// configured <see cref="ModelScale"/> — what the view should measure on
        /// the kit-model path.</summary>
        public static float NominalBodyLength => ModelLengthAtImportScale * ModelScale;

        /// <summary>#672: the truck body's width across its travel axis at the
        /// configured <see cref="ModelScale"/> — the dimension the lane budget
        /// below is spent on.</summary>
        public static float NominalBodyWidth => ModelWidthAtImportScale * ModelScale;

        /// <summary>
        /// #672: the widest body that still fits in its lane. The vehicle's centre
        /// sits <see cref="RoadLane.Offset"/> off the centerline, so only
        /// <c>RoadWidth/2 - laneOffset</c> of pavement remains outboard of it, and
        /// the body may reach half its width into that. (Written multiplier-last,
        /// like <see cref="CrosswalkSpacing"/>, so the #105 duplicate-dimension
        /// source guard reads the <c>2</c> as the multiplier it is rather than as
        /// a re-declared <see cref="WorldDimensions.SidewalkWidth"/>.)
        /// </summary>
        public const float MaxBodyWidth = (WorldDimensions.RoadWidth / 2f - RoadLane.Offset) * 2f;

        /// <summary>
        /// THE lane constraint (#672). Keeping right is only a fix if the whole
        /// body stays on the pavement once it is over there:
        ///
        ///     laneOffset + bodyWidth / 2  &lt;=  RoadWidth / 2
        ///
        /// Otherwise moving the truck out of the middle of the road would just put
        /// its outer side on the sidewalk instead — the same technically-correct-
        /// but-wrong trade the #538 "never leaves the roadway" invariant exists to
        /// rule out. Unlike <see cref="FitsBetweenCrosswalkBands"/> this one allows
        /// equality: a body exactly as wide as its lane touches the curb line but
        /// never crosses it, and nothing deadlocks at the boundary.
        /// </summary>
        public static bool FitsInsideItsLane(float bodyWidth)
        {
            return RoadLane.Offset + bodyWidth / 2f <= WorldDimensions.RoadWidth / 2f;
        }

        /// <summary>#639: how far ahead of the truck's pivot its front bumper
        /// stops short of a crosswalk band — half a body, plus the stop gap. The
        /// pivot is the centre of the body, so stopping THAT at a band's near
        /// edge would leave the whole front half overhanging it.</summary>
        public static float FrontSetbackFor(float bodyLength)
        {
            return bodyLength / 2f + CrosswalkStopGap;
        }

        /// <summary>#658: how far behind the truck's pivot its tail trails —
        /// half a body — so a band is only released once the tail, not the
        /// pivot, has cleared it.</summary>
        public static float RearSetbackFor(float bodyLength)
        {
            return bodyLength / 2f;
        }

        /// <summary>
        /// THE constraint (#660). Both setbacks are drawn from the same body, and
        /// both must fit inside the clear gap between an intersection's bands:
        ///
        ///     frontSetback + rearSetback  &lt;  ClearGapBetweenCrosswalkBands
        ///
        /// Strictly less than: at equality the truck's two ends touch both bands
        /// at the same instant, which is already enough for two oncoming trucks
        /// to hold one band each and deadlock (#658).
        /// </summary>
        public static bool FitsBetweenCrosswalkBands(float bodyLength)
        {
            return FrontSetbackFor(bodyLength) + RearSetbackFor(bodyLength)
                   < ClearGapBetweenCrosswalkBands;
        }
    }
}
