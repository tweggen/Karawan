using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using engine.streets;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * A block delimiter describes ONE edge of the block.
 *
 * QuarterGenerator traces a block as a ring, and a delimiter is a corner plus the edge
 * that leaves it: StartPoint is the corner, StreetPoint is the junction it stands on, and
 * Stroke is the street running from there to the next corner. Everything a consumer asks
 * about delims[i] - where it is, how high it is, which street it is - therefore describes
 * the same segment, the one from delims[i].StartPoint to delims[i+1].StartPoint.
 *
 * That was not true until the generator wrote all three in one call. StartPoint came from
 * the junction the trace ARRIVED at while StreetPoint and Stroke came from the one it
 * LEFT, so the delimiter described two edges a street apart, and which one a consumer got
 * depended on which field it happened to read.
 *
 * Asserted against real generated cities, because a hand-built ring can be wired to agree
 * with whatever the code does.
 */
public class QuarterDelimTests
{
    private static QuarterStore _city(string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = new FlatStreetHeight(cd);

        var store = StreetHarness.Generate(idString, size);

        return StreetHarness.GenerateQuarters(cd, store, idString);
    }


    /**
     * Angle in degrees between two plan directions, ignoring which way round they run.
     */
    private static float _angleBetween(Vector2 a, Vector2 b)
    {
        return MathF.Acos(Math.Clamp(
                   MathF.Abs(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b))),
                   0f, 1f))
               * 180f / MathF.PI;
    }


    /**
     * A delimiter's stroke is the street its own boundary segment runs along.
     *
     * Measured rather than asserted loosely, because that is what settled which way round
     * this goes: over the four baseline cities the boundary segment from delims[i] to
     * delims[i+1] is 0.00 degrees off delims[i].Stroke and 4.9 to 8.9 m from it - half a
     * carriageway - while the delimiter that used to carry that stroke, delims[i-1], is 60
     * to 76 degrees off at 35 to 51 m. All 2936 edges, no exceptions.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ADelimitersStrokeRunsAlongItsOwnBoundarySegment(string idString, float size)
    {
        var quarters = _city(idString, size);

        int nEdges = 0;
        float ownAngleMax = 0f;
        float ownOffsetMax = 0f;
        var previousAngles = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                var d = delims[i];
                var previous = delims[(i + n - 1) % n];

                Vector2 a = d.StartPoint;
                Vector2 b = delims[(i + 1) % n].StartPoint;
                Vector2 along = b - a;
                if (along.LengthSquared() < 1e-4f) continue;

                /*
                 * The stroke belongs to this corner: it leaves this delimiter's junction
                 * and arrives at the next one's, which is the far end of this very
                 * segment. Checked on identity, not on position.
                 */
                Assert.True(
                    d.Stroke.A == d.StreetPoint || d.Stroke.B == d.StreetPoint,
                    "a delimiter's stroke does not touch its own junction");
                Assert.Same(
                    delims[(i + 1) % n].StreetPoint,
                    d.Stroke.A == d.StreetPoint ? d.Stroke.B : d.Stroke.A);

                Vector2 strokeDir = d.Stroke.B.Pos - d.Stroke.A.Pos;
                ownAngleMax = Single.Max(ownAngleMax, _angleBetween(along, strokeDir));

                /*
                 * And it runs alongside rather than somewhere else parallel: the segment's
                 * midpoint is half a carriageway off the centreline.
                 */
                Vector2 mid = (a + b) / 2f;
                Vector2 ab = d.Stroke.B.Pos - d.Stroke.A.Pos;
                float t = Math.Clamp(
                    Vector2.Dot(mid - d.Stroke.A.Pos, ab) / Vector2.Dot(ab, ab), 0f, 1f);
                ownOffsetMax = Single.Max(
                    ownOffsetMax, (mid - (d.Stroke.A.Pos + t * ab)).Length());

                previousAngles.Add(
                    _angleBetween(along, previous.Stroke.B.Pos - previous.Stroke.A.Pos));

                ++nEdges;
            }
        }

        Assert.True(nEdges > 0);
        Assert.True(ownAngleMax < 0.5f,
            $"worst boundary segment is {ownAngleMax:F2} degrees off its own stroke");
        Assert.True(ownOffsetMax < 25f,
            $"worst boundary segment is {ownOffsetMax:F1} m from its own stroke, which is "
            + "further than half a carriageway");

        /*
         * The negative control, on the median rather than the worst case. The wrong answer
         * that used to be given is the PREVIOUS delimiter's stroke, and over these cities
         * it is 60 to 76 degrees off the same segment at the median - but not at the
         * minimum: two arms of a junction can be very nearly collinear, and then the two
         * strokes are parallel and no angle separates them. That is why the assertions
         * above are on junction IDENTITY, which separates them everywhere.
         */
        previousAngles.Sort();
        float previousAngleMedian = previousAngles[previousAngles.Count / 2];

        Assert.True(previousAngleMedian > 30f,
            $"the previous delimiter's stroke is {previousAngleMedian:F2} degrees off the "
            + "same segment at the median, so this city is too straight to be evidence");
    }


    /**
     * The three parts of a delimiter cannot be written apart.
     *
     * This is the whole guard. The wrong pairing compiles, leaves the plan geometry
     * exactly right and is invisible in a flat city, so what has to be impossible is
     * assembling a delimiter from two different steps of the trace - which is what an
     * object initialiser setting whichever field its author was thinking of does.
     */
    [Fact]
    public void ADelimiterHasNoPublicSetters()
    {
        foreach (string name in new[] { "StartPoint", "StreetPoint", "Stroke" })
        {
            var property = typeof(QuarterDelim).GetProperty(name);
            Assert.NotNull(property);
            Assert.True(
                property.SetMethod == null || !property.SetMethod.IsPublic,
                $"QuarterDelim.{name} can be set on its own - it must go through SetEdge "
                + "together with the other two, or a delimiter can describe two edges");
        }

        Assert.Empty(typeof(QuarterDelim)
            .GetFields(BindingFlags.Public | BindingFlags.Instance));
    }
}
