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
 * boundary edge, starting beside that edge's corner and offset onto the pavement. Each
 * segment carries a PositionDescription naming the junction it leaves and the street it
 * runs along, and SegmentNavigator rewrites those from the delimiter as the walker moves.
 *
 * One segment per corner is not a detail of taste: SegmentNavigator indexes
 * Quarter.GetDelims() with a SEGMENT index, so a route with two waypoints per edge asks a
 * delimiter list of n for element 2n-1.
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
     * measured claim rather than an assurance.
     *
     * **Superseded, not re-baselined.** This used to read
     *
     *     Assert.True((v2 - delims[i].StartPoint).Length() < 1.51f, ...)
     *
     * on the grounds that "the 1.5 m is the step onto the pavement". The step is now the
     * corner's own mitre, whose length is the offset divided by sin(half the interior
     * angle) and therefore longer than the offset at every corner that turns - bounded by
     * the block's own pavement width rather than by a constant, which is the stronger
     * statement and the one that means "still on the pavement". Measured over the four
     * baselines the waypoint stands 0.5 to 3.6 m from its corner.
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

                Assert.True(
                    (v2 - delims[i].StartPoint).Length() < q.SidewalkWidth + 1e-3f,
                    $"segment {i} starts {(v2 - delims[i].StartPoint).Length():F2} m from "
                    + $"its own corner, off a {q.SidewalkWidth} m pavement");
                Assert.True(
                    (v2 - delims[(i + 1) % n].StartPoint).Length()
                    > (v2 - delims[i].StartPoint).Length(),
                    $"segment {i} starts nearer the NEXT corner than its own");

                ++nSegments;
            }
        }

        Assert.True(nSegments > 0);
    }


    /**
     * Is this block edge on the carriageway its own stroke describes?
     *
     * StreetPoint._computeSectionArrayNoLock falls back for near collinear arms once their
     * offset lines meet more than 63 m out, and the section points it then produces are not
     * on their stroke's edge at all - 11 of the 2477 block edges of Yelukhdidru/3000, by up
     * to 62 m. That is a PLAN defect in the block outline itself, it is identical in the
     * flat city, and it is not what this file is about: those corners are named and
     * excluded rather than averaged in. Same predicate as KerbSeamTests.
     */
    private static bool _isOffItsOwnStroke(QuarterDelim d, in Vector2 a, in Vector2 b)
    {
        if (null == d.Stroke) return true;

        float Off(in Vector2 p) => Single.Abs(
            Single.Abs(Vector2.Dot(p - d.Stroke.A.Pos, d.Stroke.Normal))
            - d.Stroke.StreetWidth() / 2f);

        return Single.Max(Off(a), Off(b)) > 1.0f;
    }


    /**
     * THE gate: the ordinary citizen never leaves its own block.
     *
     * The block's outline IS the kerb, so a waypoint outside it stands in the carriageway
     * at pavement height. The shipped construction offset 1.5 m perpendicular to the edge
     * LEAVING each corner and never consulted the edge arriving at it, which puts the point
     * inside exactly when the interior angle exceeds 90 degrees - and the median block
     * corner is 90.1 to 94.0 degrees.
     *
     * Measured at the waypoints AND at ten positions along every segment, because a walker
     * walks the segments and a construction can be right at its corners and wrong between
     * them. The shipped expression is carried alongside as the baseline, so this cannot be
     * satisfied by a measurement too coarse to have seen the defect.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheLoopWalkerNeverLeavesItsOwnBlock(string idString, float size)
    {
        var (cd, _, quarters) = _terrainCity(idString, size);

        int nOutside = 0, nSamples = 0, nWasOutside = 0, nWaypoints = 0;
        float worst = 0f;
        string worstWhere = "";

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var ring = new List<Vector2>(n);
            foreach (var d in delims) ring.Add(d.StartPoint);

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            for (int i = 0; i < n; ++i)
            {
                int ip = (i + n - 1) % n;
                if (_isOffItsOwnStroke(delims[i], ring[i], ring[(i + 1) % n])
                    || _isOffItsOwnStroke(delims[ip], ring[ip], ring[i]))
                {
                    continue;
                }

                Vector3 a3 = route.Segments[i].Position - cd.Pos;
                Vector3 b3 = route.Segments[(i + 1) % n].Position - cd.Pos;
                Vector2 a = new(a3.X, a3.Z), b = new(b3.X, b3.Z);

                /*
                 * The expression that shipped, written out so this is a comparison.
                 */
                Vector2 d0 = Vector2.Normalize(ring[(i + 1) % n] - ring[i]);
                Vector2 was = ring[i] + 1.5f * new Vector2(d0.Y, -d0.X);
                if (!global::engine.streets.generation.SidewalkRing.ContainsInPlan(ring, was))
                {
                    ++nWasOutside;
                }

                ++nWaypoints;

                for (int k = 0; k <= 10; ++k)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / 10f);
                    ++nSamples;

                    if (global::engine.streets.generation.SidewalkRing.ContainsInPlan(ring, p))
                    {
                        continue;
                    }

                    ++nOutside;
                    float dOut = _distanceToRing(ring, p);
                    if (dOut > worst)
                    {
                        worst = dOut;
                        worstWhere = $"the block at {q.GetCenterPoint()}, segment {i} at "
                                     + $"t={k / 10f:F1}";
                    }
                }
            }
        }

        Assert.True(nWaypoints > 4, $"only {nWaypoints} corners were measurable");

        Assert.True(0 == nOutside,
            $"{idString}/{size}: {nOutside} of {nSamples} positions on the citizens' walk "
            + $"are outside their own block, worst {worst:F2} m out at {worstWhere}");

        /*
         * ...and the shipped construction was, at a third of its corners at least. Without
         * this the gate above passes on any city whose blocks happen to be convex enough.
         */
        Assert.True(nWasOutside * 10 > nWaypoints * 3,
            $"{idString}/{size}: the shipped expression was outside at only {nWasOutside} "
            + $"of {nWaypoints} corners, so this measurement cannot distinguish it from the "
            + "walk and proves nothing");
    }


    private static float _distanceToRing(IList<Vector2> ring, in Vector2 p)
    {
        float best = Single.MaxValue;
        for (int i = 0; i < ring.Count; ++i)
        {
            Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
            Vector2 ab = b - a;
            float l2 = ab.LengthSquared();
            float t = l2 < 1e-9f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
            best = Single.Min(best, (p - (a + t * ab)).Length());
        }

        return best;
    }


    /**
     * ...and it walks the block's OWN pavement, not a constant width of it.
     *
     * Both ends of a segment are one offset from the same edge line, so the whole segment
     * is - and the offset is half the block's own SidewalkWidth, capped at the 1.5 m that
     * shipped. A 1 m pavement cannot hold a walker 1.5 m in, and the four baseline cities
     * contain no 1 m pavement at all, so what this gate can actually catch on real data is
     * a walk that uses somebody else's width: half of 2 m is 1 m, and 1.5 m on a 2 m
     * pavement is three quarters of the way across it.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheLoopWalkerKeepsToTheBlocksOwnPavementWidth(string idString, float size)
    {
        var (cd, _, quarters) = _terrainCity(idString, size);
        int nSamples = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            float limit = global::engine.streets.generation.PavementWalk.OffsetFor(
                q.SidewalkWidth);

            var route = new QuarterLoopRouteGenerator
            {
                ClusterDesc = cd,
                Quarter = q
            }.GenerateRoute();

            for (int i = 0; i < n; ++i)
            {
                Vector2 e0 = delims[i].StartPoint, e1 = delims[(i + 1) % n].StartPoint;
                if ((e1 - e0).Length() < 1e-3f) continue;

                Vector2 d = Vector2.Normalize(e1 - e0);

                Vector3 a3 = route.Segments[i].Position - cd.Pos;
                Vector3 b3 = route.Segments[(i + 1) % n].Position - cd.Pos;
                Vector2 a = new(a3.X, a3.Z), b = new(b3.X, b3.Z);

                for (int k = 0; k <= 10; ++k)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / 10f);
                    float perp = Single.Abs(d.X * (p.Y - e0.Y) - d.Y * (p.X - e0.X));
                    ++nSamples;

                    Assert.True(perp <= limit + 1e-2f,
                        $"{idString}/{size}: on a {q.SidewalkWidth} m pavement the walk is "
                        + $"{perp:F3} m from its own kerb at t={k / 10f:F1} of segment {i}, "
                        + $"against an offset of {limit:F3}");
                }
            }
        }

        Assert.True(nSamples > 40);
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
     * The default FLAT city's loop moves in PLAN, and not by a float in height.
     *
     * **Superseded, not re-baselined.** This gate used to be
     * `AFlatCityLoopIsUnchangedFloatForFloat` and asserted all three components equal to
     * the shipped expression
     *
     *     v3This - 1.5f * Cross(Normalize(v3Next - v3This), UnitY)
     *
     * *"Both the height and the plan position, and as EQUALITY rather than within a
     * tolerance: on a flat block every corner is at the average, so the boundary
     * interpolation is h + t * 0, which is h exactly."*
     *
     * The height half of that is still true and is still asserted as equality. The plan
     * half cannot be: the defect being fixed is a plan defect, present and identical in the
     * flat city, so **every citizen's walk in the shipped flat city moves** - median 1.13 to
     * 1.50 m, p95 3.2 to 3.5 m, worst 4.11 m, and exactly 0.000 m at the 5 % of corners
     * whose two edges are collinear, where the mitre reduces to the old expression. It is
     * the fifth deliberate move of the default flat city in this work stream, after §7i,
     * §7j, §7l, §7m and §7n.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCityLoopMovesInPlanOnlyAndNotInHeight(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size);
        cd.Pos = new Vector3(1500f, 33f, -800f);

        int nSegments = 0;
        var moved = new List<float>();

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

                /*
                 * The height does not move at all, and as equality: every corner of a flat
                 * block is at the average, so the boundary interpolation is h + t * 0 and
                 * the two constants are added in the order they were added before. Moving
                 * the waypoint in plan therefore cannot move it in height.
                 */
                Assert.Equal(was.Y, route.Segments[i].Position.Y);

                var now = route.Segments[i].Position;
                float d = new Vector2(now.X - was.X, now.Z - was.Z).Length();
                moved.Add(d);

                ++nSegments;
            }
        }

        Assert.True(nSegments > 0);

        moved.Sort();
        Assert.True(moved[moved.Count / 2] > 0.4f,
            $"{idString}/{size}: the flat city's walk moved only "
            + $"{moved[moved.Count / 2]:F3} m at the median, so either the fix is not "
            + "applied here or this measurement cannot see it");
        Assert.True(moved[^1] < 6.1f,
            $"{idString}/{size}: the flat city's walk moved {moved[^1]:F2} m at the worst "
            + "corner, which is more than any block's pavement is wide");
        Assert.True(moved[0] < 0.05f,
            $"{idString}/{size}: the least moved corner still moved {moved[0]:F3} m, so "
            + "the collinear case - where the mitre reduces to the shipped expression and "
            + "nothing at all happens - is not exercised here");
    }
}
