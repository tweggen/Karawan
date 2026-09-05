using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.world;

namespace builtin.modules.satnav;


/**
 * The guideline: a flat ribbon lying on the surface the player is being sent along.
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
     *
     * ⚠️ **A lift is not a licence to be wrong by less than it.** The ribbon used to be a
     * straight chord between its lane's two junction heights while the road under it is
     * flat over each junction cap and climbs only between the section points, so it sank
     * into the carriageway by a median 0.07 to 0.19 m - more than this - and by up to
     * 9.85 m, at 43 to 50 % of positions on the shipped terrain. See NavLane.Surface.
     */
    internal const float Lift = 0.1f;


    /**
     * One quad of the ribbon, as its four corners rather than a corner and two edges.
     *
     * A parallelogram cannot carry this surface: the road's two kerbs climb between
     * different pairs of section points, so the cross fall across a stroke is a function of
     * how far along it you are - measured over the four baseline cities at 0.10 to 0.15 m
     * between the two kerbs at the median and up to 3.6 m - and a quad built from one
     * corner and two edge vectors has the same cross fall at both of its ends by
     * construction.
     *
     * Named in AddQuadXYUV's order: V00 is the corner, V10 is across, V01 is along.
     */
    internal readonly record struct Quad(Vector3 V00, Vector3 V10, Vector3 V01, Vector3 V11);


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
     * The height of the surface under one point of a lane, before the lift.
     *
     * A CAR lane knows the carriageway it runs along and asks it, so the ribbon follows the
     * road's own flat-ramp-flat profile instead of cutting the corner off it. At either
     * junction the two answers are the same float - RoadSurface clamps its chord fraction,
     * so a position over a junction cap gets that junction's own height, which is exactly
     * what SurfaceHeightOf returns there - so this changes the middle of a lane and not its
     * ends.
     *
     * A PEDESTRIAN lane interpolates between its two junctions instead, and that is not a
     * fallback: a pavement lane runs corner to corner along a block edge, and the block
     * floor's outline is the straight segment between exactly those two corner heights. The
     * chord IS the surface there. Deliberately not asking nl.Surface even if something ever
     * puts one on a pedestrian lane, because the pavement is a kerb above the carriageway
     * and the two are not the same surface.
     *
     * @param t
     *     How far along the lane, for the chord. Ignored when the carriageway answers.
     */
    internal static float SurfaceHeightAt(
        NavLane nl, in Vector3 v3Plan, float t, TransportationType transportType)
    {
        if (TransportationType.Pedestrian != transportType && nl.Surface.HasValue)
        {
            return nl.Surface.Value.SurfaceHeightAt(new Vector2(v3Plan.X, v3Plan.Z));
        }

        return Single.Lerp(
            SurfaceHeightOf(nl.Start, transportType),
            SurfaceHeightOf(nl.End, transportType),
            t);
    }


    /**
     * How far along this lane, as a fraction, the profile of the road under it changes
     * slope - so that the ribbon can be straight between those points and bend only there.
     *
     * The road's profile along a stroke is flat at the A junction's height over its cap,
     * climbing between the section points, then flat at the B junction's height: four
     * breaks, one per section point, since the two sides do not share theirs. A lane
     * subdivided at 50 m may contain none of them, all four, or anything between, and the
     * ones outside it are dropped rather than clamped - a break at the very end of a span
     * is not a break.
     *
     * ...and then within each of those pieces, as many more as it takes to keep the ribbon's
     * OWN quads on the surface. Between two section points the road is a hyperbolic
     * paraboloid - the two kerbs climb at different rates, so the cross fall varies along
     * the stroke - and a quad split into two triangles departs from such a surface by a
     * quarter of the twist it spans. The road bounds that for its own rows
     * (RoadSurface.MaxRowSpan); a ribbon quad is 4 m of the same surface and carries 4 m
     * worth of the same twist, and until this was added it did not bound it at all. It was
     * measured, with the road already held to RoadSurface.MaxSag: the guideline was still
     * 0.85 m off the carriageway at the worst position of five cities and 0.22 m at p99,
     * every metre of it the ribbon's own quads and none of it the road's.
     *
     * The two are cut at different distances on purpose - see RoadSurface.MaxSpanAcross.
     *
     * A LEVEL surface has no breaks at all and gets none, which is what keeps a flat city's
     * ribbon the same four vertices per lane it has always been.
     */
    internal static void BreaksAlong(NavLane nl, List<float> into)
    {
        into.Clear();
        if (!nl.Surface.HasValue) return;

        var surface = nl.Surface.Value;
        if (surface.IsLevel) return;

        float dStart = surface.AxialAt(new Vector2(nl.Start.Position.X, nl.Start.Position.Z));
        float dEnd = surface.AxialAt(new Vector2(nl.End.Position.X, nl.End.Position.Z));
        float span = dEnd - dStart;
        if (Single.Abs(span) < engine.streets.generation.RoadSurface.MinSpan) return;

        /*
         * The two edges of the ribbon, half a width either side of the lane, because where
         * each of them crosses a junction's seam is a break in the surface along it and the
         * two cross in different places.
         */
        Vector3 v3Along = nl.End.Position - nl.Start.Position;
        Vector3 vu3Right = Vector3.Normalize(new(v3Along.Z, 0f, -v3Along.X));
        Vector3 v3Mid = nl.Start.Position + 0.5f * v3Along;
        Vector3 v3Rail0 = v3Mid + (Width / 2f) * vu3Right;
        Vector3 v3Rail1 = v3Mid - (Width / 2f) * vu3Right;

        Span<float> breaks = stackalloc float[engine.streets.generation.RoadSurface.NBreakpoints];
        surface.BreakpointsBetween(
            new Vector2(v3Rail0.X, v3Rail0.Z), new Vector2(v3Rail1.X, v3Rail1.Z), breaks);

        for (int i = 0; i < breaks.Length; ++i)
        {
            float t = (breaks[i] - dStart) / span;
            if (t <= 1e-4f || t >= 1f - 1e-4f) continue;
            into.Add(t);
        }

        into.Sort();

        for (int i = into.Count - 1; i > 0; --i)
        {
            if (into[i] - into[i - 1] < 1e-4f) into.RemoveAt(i);
        }

        _subdivideAlong(surface, into, span);
    }


    /**
     * The most quads one lane's ribbon may be cut into.
     *
     * A bound on the arithmetic and not on the geometry, exactly as
     * GenerateClusterStreetsOperator.MaxRowsPerTextureLength is: what is asked for is a
     * lane's length over RoadSurface.MaxSpanAcross, and a stroke whose two section points
     * nearly coincide on one side has a nearly unbounded slope there. Over the five
     * baseline cities on the shipped terrain the most quads a lane actually comes out in is
     * 40, so this does not bind - and ALaneOverAClimbingRoadIsBrokenWhereTheRoadBreaks
     * asserts strictly below it, since a lane that reached the cap would be one whose ribbon
     * is silently coarser than MaxSag rather than one that is merely expensive.
     */
    internal const int MaxQuadsPerLane = 64;


    /**
     * Insert enough extra breaks that no piece of the ribbon spans more of the surface's
     * twist than MaxSpanAcross allows.
     *
     * @param span
     *     The lane's own axial extent along the stroke, signed - a lane may run either way
     *     along its carriageway, and only its magnitude matters here.
     */
    private static void _subdivideAlong(
        in engine.streets.generation.RoadSurface surface, List<float> into, float span)
    {
        float maxSpan = surface.MaxSpanAcross(Width);
        if (!Single.IsFinite(maxSpan) || maxSpan <= 0f) return;

        float axial = Single.Abs(span);

        /*
         * Built into a second list and swapped back, because the extra breaks belong
         * BETWEEN the ones already there and a list cannot be walked while it grows.
         */
        var pieces = new List<float>(into);
        into.Clear();

        float t0 = 0f;
        for (int i = 0; i <= pieces.Count; ++i)
        {
            float t1 = i < pieces.Count ? pieces[i] : 1f;

            int nSub = Math.Clamp(
                (int)Single.Ceiling((t1 - t0) * axial / maxSpan), 1, MaxQuadsPerLane);

            for (int sub = 1; sub < nSub; ++sub)
            {
                into.Add(t0 + (t1 - t0) * ((float)sub / (float)nSub));
            }

            if (i < pieces.Count) into.Add(t1);
            t0 = t1;
        }
    }


    /**
     * The ribbon for one lane, as one quad per straight piece of the road under it.
     *
     * Every corner takes the height of the surface at its OWN plan position, so a ribbon
     * over a climbing road climbs with it and a ribbon over a road that cross-falls tilts
     * with that too - no extra term, and nothing to keep in step with the road's own slope.
     */
    internal static void QuadsFor(
        NavLane nl, TransportationType transportType, List<Quad> into, List<float> scratch)
    {
        into.Clear();

        Vector3 v3Start = nl.Start.Position;
        Vector3 v3End = nl.End.Position;

        Vector3 v3Along = v3End - v3Start;
        Vector3 vu3Right = Vector3.Normalize(new(v3Along.Z, 0f, -v3Along.X));

        /*
         * The two rails, written as the old single quad wrote them - a corner half a width
         * to one side and one full width across - so that a lane with no break in it comes
         * out at the same floats it always did rather than at the same place to within a
         * rounding.
         */
        Vector3 v3Left = v3Start + (Width / 2f) * vu3Right;
        Vector3 v3Right = v3Left + -Width * vu3Right;

        BreaksAlong(nl, scratch);

        float t0 = 0f;
        for (int i = 0; i <= scratch.Count; ++i)
        {
            float t1 = i < scratch.Count ? scratch[i] : 1f;

            into.Add(new Quad(
                _corner(nl, v3Left, v3Along, t0, transportType),
                _corner(nl, v3Right, v3Along, t0, transportType),
                _corner(nl, v3Left, v3Along, t1, transportType),
                _corner(nl, v3Right, v3Along, t1, transportType)));

            t0 = t1;
        }
    }


    /**
     * The whole guideline: every lane of a route, as one mesh.
     *
     * Here rather than as a loop at the call site, and that is not tidiness. Mutation
     * testing found that drawing only the FIRST quad of each lane - i.e. keeping every
     * corner right and cutting straight across the profile between them, which is the
     * defect this file exists to remove - passed the entire suite, because
     * ToSomewhere._onJunctions runs inside a queued main-thread action in a module that
     * needs a booted engine and is covered by a source scan, and a scan sees the name of a
     * call and not how many of its results are used.
     */
    internal static engine.joyce.Mesh MeshFor(
        IReadOnlyList<NavLane> lanes, TransportationType transportType)
    {
        var jMesh = engine.joyce.Mesh.CreateListInstance("waypoints");

        var quads = new List<Quad>();
        var breaks = new List<float>();

        foreach (var nl in lanes)
        {
            QuadsFor(nl, transportType, quads, breaks);

            foreach (var q in quads)
            {
                engine.joyce.mesh.Tools.AddQuadCornersUV(jMesh,
                    q.V00, q.V10, q.V01, q.V11,
                    Vector2.Zero, Vector2.Zero, Vector2.Zero);
            }
        }

        return jMesh;
    }


    private static Vector3 _corner(
        NavLane nl, in Vector3 v3Rail, in Vector3 v3Along, float t,
        TransportationType transportType)
    {
        Vector3 p = v3Rail + t * v3Along;

        return p with { Y = SurfaceHeightAt(nl, p, t, transportType) + Lift };
    }
}
