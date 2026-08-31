using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.streets;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * Which side of a sidewalk lane a walker actually stands on.
 *
 * A sidewalk lane runs along a block's kerb line - GenerateNavMapOperator traces one
 * between every pair of adjacent block corners - so a walker on the lane's centre line is
 * standing on the kerb. PedestrianRoute.WaypointFor steps them 1.5 m off it, and which way
 * it steps decides whether they are on the pavement or in the road.
 *
 * It used to step 1.5 m to the RIGHT OF TRAVEL, unconditionally. Sidewalk lanes are created
 * in both directions over the same ground (_createBidirectionalLanes), so that put exactly
 * one lane of every pair in the carriageway, at pavement height, and which one the walker
 * got depended on which way round the block the A* routed. Present in the flat city too.
 *
 * These tests measure containment in the real block polygons of real generated cities,
 * because that is the only thing the question is actually about - metric separation is no
 * use here, since a point 1.5 m outside one block may be well inside its neighbour.
 */
public class PedestrianKerbSideTests
{
    private static (ClusterDesc, QuarterStore) _city(
        string idString, float size, Func<float, float, float> fHeight)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = null == fHeight
            ? new FlatStreetHeight(cd)
            : new FuncStreetHeight(fHeight);

        var store = StreetHarness.Generate(idString, size);

        return (cd, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    public static IEnumerable<object[]> Cities()
    {
        foreach (var (idString, size) in new[]
                 {
                     ("seed000", 800f), ("Yelukhdidru", 1500f),
                     ("seed000", 1500f), ("Yelukhdidru", 3000f)
                 })
        {
            yield return new object[] { idString, size };
        }
    }


    /**
     * A lane along one block edge, built the way the operator builds it.
     *
     * The kerb side comes from the operator's own two helpers rather than from a copy of
     * their arithmetic, so that changing how a block's winding is decided changes this test
     * too instead of leaving it asserting the old answer.
     */
    private static (NavLane forth, NavLane back) _lanePair(
        List<QuarterDelim> delims, int i, bool isCcw)
    {
        int n = delims.Count;
        Vector2 a = delims[i].StartPoint, b = delims[(i + 1) % n].StartPoint;

        var njA = NavJunction.At(new Vector3(a.X, 0f, a.Y), 0f);
        var njB = NavJunction.At(new Vector3(b.X, 0f, b.Y), 0f);

        Vector3 kerb = GenerateNavMapOperator._inwardOf(a, b, isCcw);

        return (
            new NavLane { Start = njA, End = njB, KerbSide = kerb },
            new NavLane { Start = njB, End = njA, KerbSide = kerb });
    }


    /**
     * Both directions of every sidewalk lane step onto the block, not off it.
     *
     * This is the whole defect: the two directions cover the same ground, so the offset has
     * to survive being walked backwards. Asserted over every edge of every block of four
     * generated cities.
     *
     * Measured along the lane rather than at its far end. A waypoint sits AT the end
     * junction, which is a block corner, and a point 1.5 m off a corner perpendicular to one
     * of its two edges is outside the block whenever that corner is sharp enough - for the
     * same reason a pavement inset has to ramp back to the kerb there (SidewalkRing). That
     * is corner geometry and it is true of any offset at all, including the correct one; what
     * this test is about is which SIDE, so it asks where the offset puts a walker on the
     * lane proper.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void BothDirectionsOfALaneStandOnTheBlock(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);

        int nLanes = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            bool isCcw = GenerateNavMapOperator._isCcwInPlan(delims);

            for (int i = 0; i < delims.Count; ++i)
            {
                var (forth, back) = _lanePair(delims, i, isCcw);
                if (forth.KerbSide == Vector3.Zero) continue;

                foreach (var lane in new[] { forth, back })
                {
                    Vector3 w = PedestrianRoute.WaypointFor(lane);

                    /*
                     * The same offset, applied halfway along the lane instead of at its end.
                     */
                    Vector3 mid = 0.5f * (lane.Start.Position + lane.End.Position)
                                  + (w - lane.End.Position with { Y = lane.End.WalkingHeight });

                    Assert.True(_containsInPlan(delims, new Vector2(mid.X, mid.Z)),
                        $"{idString}/{size}: a walker on the block at {q.GetCenterPoint()} "
                        + $"stands at {mid.X}, {mid.Z} - outside the block, i.e. in the road");
                    ++nLanes;
                }
            }
        }

        Assert.True(nLanes > 40);
    }


    /**
     * ...and the old rule really did put one of them in the road.
     *
     * Without this, the test above passes for any offset small enough to stay inside the
     * block - including zero, which would leave every walker balanced on the kerb. The old
     * expression is written out here and measured on the same edges.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheOldFixedHandPutHalfOfThemInTheRoad(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);

        int nOutside = 0, nLanes = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            bool isCcw = GenerateNavMapOperator._isCcwInPlan(delims);

            for (int i = 0; i < delims.Count; ++i)
            {
                var (forth, back) = _lanePair(delims, i, isCcw);
                if (forth.KerbSide == Vector3.Zero) continue;

                foreach (var lane in new[] { forth, back })
                {
                    /*
                     * PedestrianRoute.WaypointFor as it stood: 1.5 m to the right of travel,
                     * measured at the same halfway point as above so that the comparison is
                     * between the two rules and not between two places on the lane.
                     */
                    Vector3 dir = Vector3.Normalize(lane.End.Position - lane.Start.Position);
                    Vector3 right = Vector3.Cross(dir, Vector3.UnitY);
                    Vector3 mid = 0.5f * (lane.Start.Position + lane.End.Position)
                                  + right * PedestrianRoute.SidewalkOffset;

                    if (!_containsInPlan(delims, new Vector2(mid.X, mid.Z)))
                    {
                        ++nOutside;
                    }

                    ++nLanes;
                }
            }
        }

        /*
         * Exactly one of each pair, so half.
         */
        Assert.True(nOutside > 0.45f * nLanes,
            $"only {nOutside} of {nLanes} lanes of {idString}/{size} were in the road under "
            + "the old rule, so this no longer describes the defect that was fixed");
    }


    /**
     * A lane with no kerb side keeps the centre line.
     *
     * Pedestrian crossings are lanes too, and a crossing is in the carriageway by
     * definition: there is no pavement to step onto, and offsetting it to either hand moves
     * it off the crossing it exists to be. Car lanes are the same - nothing offsets them,
     * but nothing should start to either.
     */
    [Fact]
    public void ALaneWithNoKerbSideIsWalkedDownItsMiddle()
    {
        var njA = NavJunction.At(new Vector3(0f, 0f, 0f), 10f);
        var njB = NavJunction.At(new Vector3(20f, 0f, 0f), 14f);

        var lane = new NavLane { Start = njA, End = njB };

        Assert.Equal(Vector3.Zero, lane.KerbSide);

        Vector3 w = PedestrianRoute.WaypointFor(lane);

        Assert.Equal(njB.Position.X, w.X, 4);
        Assert.Equal(njB.Position.Z, w.Z, 4);
        Assert.Equal(njB.WalkingHeight, w.Y, 4);
    }


    /**
     * The two pedestrian systems agree on which side the pavement is.
     *
     * builtin.tools.QuarterLoopRouteGenerator - the ordinary citizen's loop walker - offsets
     * by -1.5 * Cross(forward, UnitY) along the block in its traced order, and has always
     * been right. The satnav walker is the one that was wrong, so the two are compared here
     * directly rather than each being checked against the polygon alone: two systems walking
     * the same pavement on opposite sides is the shape of the defect, and it is what a
     * source scan of either one on its own would miss.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheSatnavWalkerAndTheLoopWalkerKeepToTheSameSide(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);
        int nEdges = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            bool isCcw = GenerateNavMapOperator._isCcwInPlan(delims);

            for (int i = 0; i < n; ++i)
            {
                Vector2 a = delims[i].StartPoint, b = delims[(i + 1) % n].StartPoint;
                if ((b - a).Length() < 1e-3f) continue;

                /*
                 * QuarterLoopRouteGenerator's own expression, over the same edge.
                 */
                Vector3 v3This = new(a.X, 0f, a.Y);
                Vector3 v3Next = new(b.X, 0f, b.Y);
                Vector3 vu3Forward = Vector3.Normalize(v3Next - v3This);
                Vector3 vu3Right = Vector3.Cross(vu3Forward, Vector3.UnitY);
                Vector3 loop = -1.5f * vu3Right;

                Vector3 satnav = GenerateNavMapOperator._inwardOf(a, b, isCcw)
                                 * PedestrianRoute.SidewalkOffset;

                Assert.True(Vector3.Dot(loop, satnav) > 0f,
                    $"{idString}/{size}: on the block at {q.GetCenterPoint()} the loop "
                    + $"walker steps {loop} and the satnav walker {satnav} - opposite sides "
                    + "of the same pavement");
                ++nEdges;
            }
        }

        Assert.True(nEdges > 40);
    }


    /**
     * The waypoint is not derived from the direction of travel any more.
     *
     * A source scan, because the property is about what the expression may DEPEND on rather
     * than about a value: re-deriving the side from Cross(laneDir, UnitY) inside WaypointFor
     * would reproduce the bug exactly and every containment test above would still pass for
     * the forth direction. The lane's own side is the only thing that survives being walked
     * backwards.
     */
    [Fact]
    public void TheWaypointDoesNotTakeItsSideFromTheDirectionOfTravel()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/builtin/modules/satnav/PedestrianRoute.cs";
        Assert.True(File.Exists(path), $"could not find PedestrianRoute at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("lane.KerbSide", source);
        Assert.DoesNotContain("Vector3.Cross", source);
    }


    /**
     * Crossing-number point in polygon, over the block's corners in plan.
     */
    private static bool _containsInPlan(List<QuarterDelim> delims, Vector2 p)
    {
        int n = delims.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = delims[i].StartPoint, b = delims[j].StartPoint;
            if (a.Y > p.Y != b.Y > p.Y
                && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
