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
}
