using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.tools;


/**
 * The walk round a block, and what each of its segments says it is.
 *
 * QuarterLoopRouteGenerator turns a block into the loop NPCs walk: one segment per
 * boundary edge, starting at that edge's corner and offset 1.5 m onto the pavement. Each
 * segment carries a PositionDescription naming the junction it leaves and the street it
 * runs along, and SegmentNavigator rewrites those from the delimiter as the walker moves.
 *
 * Those names were one edge out. The generator labelled the segment leaving corner i with
 * delimiter i's junction and stroke, and a delimiter's junction and stroke used to belong
 * to the edge ARRIVING at its corner - so an NPC on the pavement of one street reported
 * itself on the street before it, at a junction it had already left. Nothing in the game
 * reads either field today, which is exactly why it went unnoticed; the fix is in the
 * delimiter, and this is where it becomes visible.
 */
public class QuarterLoopRouteTests
{
    private static (ClusterDesc, QuarterStore) _city(string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = new FlatStreetHeight(cd);

        var store = StreetHarness.Generate(idString, size);

        return (cd, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * Every segment of the loop names the junction it starts at and the street it runs
     * along.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void EverySegmentNamesTheStreetItRunsAlong(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size);

        int nSegments = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            Assert.Equal(n, route.Segments.Count);

            for (int i = 0; i < n; ++i)
            {
                var pod = route.Segments[i].PositionDescription;
                Assert.Equal(i, pod.QuarterDelimIndex);

                /*
                 * The corner this segment starts at, which the walker stands on: a
                 * section point of the junction the segment names. Identity, not
                 * proximity - a neighbouring junction can be 25 m away.
                 */
                Assert.Contains(
                    pod.StreetPoint.GetSectionArray(),
                    s => (s - delims[i].StartPoint).LengthSquared() < 1e-4f);

                /*
                 * And the street runs from there to the corner the segment ends at.
                 */
                Assert.True(
                    pod.Stroke.A == pod.StreetPoint || pod.Stroke.B == pod.StreetPoint,
                    "the segment's street does not touch the junction it leaves");
                Assert.Same(
                    delims[(i + 1) % n].StreetPoint,
                    pod.Stroke.A == pod.StreetPoint ? pod.Stroke.B : pod.Stroke.A);

                ++nSegments;
            }
        }

        Assert.True(nSegments > 0);
    }


    /**
     * A segment starts beside its own corner and runs to the next one.
     *
     * The plan geometry of the loop is what actually moves NPCs, and it is unchanged by
     * the delimiter correction - checked here so that "only the labels moved" is a
     * measured claim rather than an assurance. The 1.5 m is the step onto the pavement.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    public void ASegmentStartsBesideItsOwnCorner(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size);
        cd.Pos = new Vector3(1500f, 33f, -800f);

        int nSegments = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            for (int i = 0; i < n; ++i)
            {
                var v3 = route.Segments[i].Position - cd.Pos;
                var v2 = new Vector2(v3.X, v3.Z);

                Assert.True((v2 - delims[i].StartPoint).Length() < 1.51f,
                    $"segment {i} starts {(v2 - delims[i].StartPoint).Length():F2} m from "
                    + "its own corner");
                Assert.True(
                    (v2 - delims[(i + 1) % n].StartPoint).Length()
                    > (v2 - delims[i].StartPoint).Length(),
                    $"segment {i} starts nearer the NEXT corner than its own");

                ++nSegments;
            }
        }

        Assert.True(nSegments > 0);
    }


    private static (ClusterDesc, StrokeStore, QuarterStore) _terrainCity(
        string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var store = StreetHarness.Generate(idString, size);
        cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);

        return (cd, store, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    public static IEnumerable<object[]> Cities()
    {
        foreach (var (idString, size) in new[]
                 {
                     ("seed000", 500f), ("Yelukhdidru", 800f),
                     ("seed000", 1500f), ("Yelukhdidru", 3000f)
                 })
        {
            yield return new object[] { idString, size };
        }
    }


    /**
     * The ordinary city citizen walks on the PAVEMENT, not on the block's pad.
     *
     * This is the loop every citizen created by CharacterCreator walks, so it is the
     * commonest walker in the game, and its height came from Quarter.GroundHeightAt - a
     * least squares plane through the block's corner heights. A block is up to 150 m across
     * with 13 m between its highest and lowest corner, so the plane is the surface only in
     * the middle, where nobody walks, and parts company from it at the kerb, where this
     * walker stands.
     *
     * Measured against the block floor's OWN triangles at the loop's own waypoints, over the
     * four baseline cities on the shipped terrain. The pad is carried alongside as the
     * baseline, so "the walker is on the pavement" cannot be satisfied by a measurement that
     * could not have told the difference.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheLoopWalkerStandsOnThePavementAndNotOnThePad(string idString, float size)
    {
        var (cd, _, quarters) = _terrainCity(idString, size);

        var now = new List<float>();
        var pad = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var tris = BlockFloor.CapOf(q);
            if (0 == tris.Count) continue;

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            for (int i = 0; i < n; ++i)
            {
                Vector3 w = route.Segments[i].Position - cd.Pos;

                float? h = BlockFloor.SurfaceAt(tris, new Vector2(w.X, w.Z));
                if (!h.HasValue) continue;

                now.Add(w.Y - h.Value);
                pad.Add(q.GroundHeightAt(delims[i].StartPoint)
                        + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset
                        - h.Value);
            }
        }

        Assert.True(now.Count > 4, $"only {now.Count} waypoints landed on a block floor");

        float p05 = BlockFloor.Percentile(now, 0.05f);
        float p95 = BlockFloor.Percentile(now, 0.95f);

        Assert.True(p05 > -0.5f && p95 < 0.5f,
            $"{idString}/{size}: the loop walker is {p05:F2} m below the block floor at p05 "
            + $"and {p95:F2} m above it at p95, over {now.Count} waypoints");

        /*
         * ...and the pad is not. Without this the assertion above would also pass on a flat
         * city, or on any measurement too coarse to see the difference.
         */
        Assert.True(BlockFloor.Percentile(pad, 0.05f) < -1.5f,
            $"{idString}/{size}: the pad was only "
            + $"{BlockFloor.Percentile(pad, 0.05f):F2} m below the floor at p05, so this "
            + "measurement cannot distinguish it from the pavement and proves nothing");
    }


    /**
     * The two pedestrian systems name the SAME height at the same block corner.
     *
     * builtin.modules.satnav gives a sidewalk lane's junction the height of its delimiter's
     * own StreetPoint (GenerateNavMapOperator.SidewalkJunctionFor), and the loop generator
     * now takes the block boundary's height at its own waypoint. Those have to agree, and
     * their disagreement is the shape of every defect this pair has had: §7g found them
     * offsetting to OPPOSITE SIDES of the same kerb, and the height was the same story one
     * layer down.
     *
     * Asserted at the corner itself rather than at the offset waypoint, because that is
     * where the two are the same quantity; a 1.5 m step into the corner ramp is a different
     * point on the surface and is measured by the test above.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheTwoPedestrianSystemsAgreeAtABlockCorner(string idString, float size)
    {
        var (_, _, quarters) = _terrainCity(idString, size);
        int nCorners = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            foreach (var delim in delims)
            {
                float satnav = global::builtin.modules.satnav.desc.NavJunction.WalkingHeightOf(
                    q.CornerGroundHeightAt(delim));
                float loop = global::engine.streets.generation.BuildingFooting.PavementHeightAt(
                    q, delim.StartPoint);

                Assert.True(Single.Abs(satnav - loop) < 1e-3f,
                    $"{idString}/{size}: at the corner {delim.StartPoint} of the block at "
                    + $"{q.GetCenterPoint()} the satnav walker stands at {satnav:F3} and "
                    + $"the loop walker at {loop:F3}");

                ++nCorners;
            }
        }

        Assert.True(nCorners > 8);
    }


    /**
     * The default FLAT city's loop is unchanged, float for float.
     *
     * Both the height and the plan position, and as EQUALITY rather than within a
     * tolerance: on a flat block every corner is at the average, so the boundary
     * interpolation is `h + t * 0`, which is h exactly, and the two constants are added in
     * the same order they were. The direction is taken in plan now instead of at a common
     * height, and that too is the same vector - both ends always had the same Y.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCityLoopIsUnchangedFloatForFloat(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size);
        cd.Pos = new Vector3(1500f, 33f, -800f);

        int nSegments = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            for (int i = 0; i < n; ++i)
            {
                var dlThis = delims[i];
                var dlNext = delims[(i + 1) % n];

                /*
                 * The expression that shipped, written out here so that this is a
                 * comparison and not a restatement.
                 */
                float wasH = q.GroundHeightAt(dlThis.StartPoint)
                             + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;

                var v3This = new Vector3(dlThis.StartPoint.X, wasH, dlThis.StartPoint.Y);
                var v3Next = new Vector3(dlNext.StartPoint.X, wasH, dlNext.StartPoint.Y);
                var vu3Right = Vector3.Cross(
                    Vector3.Normalize(v3Next - v3This), Vector3.UnitY);
                Vector3 was = v3This - 1.5f * vu3Right + cd.Pos;

                Assert.Equal(was.X, route.Segments[i].Position.X);
                Assert.Equal(was.Y, route.Segments[i].Position.Y);
                Assert.Equal(was.Z, route.Segments[i].Position.Z);

                ++nSegments;
            }
        }

        Assert.True(nSegments > 0);
    }
}
