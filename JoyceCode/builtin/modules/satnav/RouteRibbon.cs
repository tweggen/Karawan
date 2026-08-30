using System.Numerics;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.world;

namespace builtin.modules.satnav;


/**
 * The guideline: one flat quad per lane of a route, lying on the surface the player is
 * being sent along.
 *
 * Here rather than inline in ToSomewhere._onJunctions because that runs inside a queued
 * main-thread action in a module that needs a booted engine, a physics world and the
 * satnav module - so inline is where nothing can check the arithmetic, which is how a
 * ribbon spent its life half a metre above the road.
 */
internal static class RouteRibbon
{
    /**
     * How wide the ribbon is, centred on the lane.
     */
    internal const float Width = 4f;


    /**
     * How far above the surface the ribbon is drawn, to keep it out of the road it lies on.
     *
     * The window asks for a SIXTEEN bit depth buffer (Sdl3WindowBackend), and the play
     * camera runs near = 1, far = sqrt(3) * 1000 + 100. The depth quantum on a coplanar
     * surface is then about z squared over 65535: 6 mm at 20 m, 38 mm at 50 m, 0.15 m at
     * 100 m, 0.6 m at 200 m. So no fixed lift can keep a long route off the road at its far
     * end, and the choice is only about the near end, which is the part a driver reads.
     *
     * 0.1 m holds out to about 80 m and is a tenth of the hover clearance the player's own
     * ship keeps above the same surface, so it cannot read as floating. Beyond that the
     * ribbon may shimmer against the road - that is depth precision, not height, and the
     * honest fix for it is a 24 bit depth buffer.
     *
     * What was there instead was the ribbon at the junctions' own navigation height,
     * ClusterNavigationHeight above the ground, with a flat 0.5 m taken off by the parent
     * transform: 2.5 m against a road surface at 2.0. That is a lift of half a metre in
     * the flat game too, which is what a fixed z-fighting margin looks like when it is
     * applied to the vehicle hover reference rather than to the surface.
     */
    internal const float Lift = 0.1f;


    /**
     * Height of the surface a junction is drawn on, by what is travelling it.
     *
     * A junction carries the GROUND under it, deliberately, so that each consumer adds its
     * own offset. A car lane's surface is the carriageway; a pedestrian lane's is the
     * pavement, which is one kerb higher because the block floor is extruded that far. The
     * one thing this must not do is start from Position, which is the ground plus
     * ClusterNavigationHeight - the vehicle HOVER reference, and not a surface at all.
     */
    internal static float SurfaceHeightOf(NavJunction nj, TransportationType transportType)
        => TransportationType.Pedestrian == transportType
            ? NavJunction.WalkingHeightOf(nj.GroundHeight)
            : nj.GroundHeight + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * Where a junction's end of the ribbon sits: its own plan position, at the height of
     * the surface there plus the lift.
     */
    internal static Vector3 PointOn(NavJunction nj, TransportationType transportType)
        => nj.Position with { Y = SurfaceHeightOf(nj, transportType) + Lift };


    /**
     * The quad for one lane, as AddQuadXYUV takes it: a corner and two edge vectors.
     *
     * Both ends take their OWN junction's height and the along-vector is the difference of
     * the two, so over a road that climbs the ribbon climbs with it - no extra term, and
     * nothing to keep in step with the road's own slope.
     *
     * @param v3Origin
     *     One corner, half a width to the left of the lane's start.
     * @param v3Across
     *     To the other side, a full width.
     * @param v3Along
     *     To the lane's far end.
     */
    internal static void QuadFor(
        NavLane nl, TransportationType transportType,
        out Vector3 v3Origin, out Vector3 v3Across, out Vector3 v3Along)
    {
        Vector3 v3Start = PointOn(nl.Start, transportType);
        Vector3 v3End = PointOn(nl.End, transportType);

        Vector3 v3Plan = nl.End.Position - nl.Start.Position;
        Vector3 vu3Right = Vector3.Normalize(new(v3Plan.Z, 0f, -v3Plan.X));

        v3Origin = v3Start + (Width / 2f) * vu3Right;
        v3Across = -Width * vu3Right;
        v3Along = v3End - v3Start;
    }
}
