using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Where a pedestrian stands when walking round a block.
 *
 * The block's outline IS the kerb, so "on the pavement" is one statement about a plan
 * position: strictly inside the ring, and no further from it than the pavement is wide.
 * This file drives that on fixtures whose shape is chosen rather than found, because the
 * two shapes that decide the answer - an ACUTE corner and a REFLEX one - are exactly the
 * ones the generated cities supply in numbers too small, or in one case not at all, to be
 * measured out of them. The whole-city measurements are in QuarterLoopRouteTests.
 */
public class PavementWalkTests
{
    /**
     * The offset that shipped, and the construction that placed it: 1.5 m along the inward
     * normal of the edge LEAVING the corner, with the arriving edge never consulted.
     *
     * Written out here rather than referred to, so that everything below is a comparison
     * against the old expression and not a restatement of the new one.
     */
    private static Vector2 _asShipped(IList<Vector2> ring, int i)
    {
        Vector2 d = Vector2.Normalize(ring[(i + 1) % ring.Count] - ring[i]);

        return ring[i] + 1.5f * new Vector2(d.Y, -d.X);
    }


    /**
     * A block with a chosen interior angle at its FIRST corner, traced clockwise - the
     * order QuarterGenerator traces blocks in.
     *
     * A fan about that corner, so the same construction covers the whole range including
     * the reflex half: the corner is the apex of a sector of `degrees`, and the rest of the
     * ring is that sector's arc. A triangle cannot do this - it has no reflex corner at any
     * shape - and a reflex corner is 6 to 16 % of what the generator actually produces.
     */
    private static List<Vector2> _wedge(float degrees, float r = 60f)
    {
        var cw = new List<Vector2> { new(0f, 0f) };

        for (int k = 4; k >= 0; --k)
        {
            float t = degrees * k / 4f * Single.Pi / 180f;
            cw.Add(new Vector2(r * Single.Cos(t), r * Single.Sin(t)));
        }

        return cw;
    }


    private static float _interiorAngleAt(IList<Vector2> ring, int i)
    {
        int n = ring.Count;
        Vector2 a = Vector2.Normalize(ring[i] - ring[(i + n - 1) % n]);
        Vector2 d = Vector2.Normalize(ring[(i + 1) % n] - ring[i]);

        bool ccw = SidewalkRing.SignedArea2Of(ring) > 0f;
        float sin = a.X * d.Y - a.Y * d.X;
        if (ccw) sin = -sin;

        float deg = Single.Atan2(-sin, Vector2.Dot(-a, d)) * 180f / Single.Pi;

        return deg < 0f ? deg + 360f : deg;
    }


    private static float _distToRing(IList<Vector2> ring, in Vector2 p)
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
     * THE thing the write-up had backwards: it is the ACUTE corner that fails.
     *
     * CITY-3D-OPEN-POINTS and §7m both recorded *"at an interior angle over 90 degrees it
     * lands past the arriving edge"*. It is the other way round, and it has to be: the
     * shipped point sits one offset along the inward normal of the LEAVING edge, so it is on
     * the inward side of the ARRIVING edge exactly when the two inward normals agree, and
     * their dot product is -cos(t). That is positive for t above 90 degrees.
     *
     * The measured symptom could not distinguish the two readings - the median block corner
     * is 90.1 to 94.0 degrees, so either reading predicts "about half" - which is exactly
     * how the direction of an inequality survives being written down twice. A sweep settles
     * it, and it is a sweep rather than one example because one example is what got it
     * wrong.
     */
    [Fact]
    public void ItIsTheAcuteCornerThatPutsTheShippedWaypointInTheRoad()
    {
        int nAcute = 0, nObtuse = 0;

        for (int deg = 10; deg <= 170; deg += 5)
        {
            if (Single.Abs(deg - 90) < 1) continue;

            var ring = _wedge(deg);
            Assert.Equal(deg, _interiorAngleAt(ring, 0), 2);

            bool inside = SidewalkRing.ContainsInPlan(ring, _asShipped(ring, 0));

            if (deg < 90)
            {
                Assert.False(inside,
                    $"the shipped waypoint is INSIDE at an acute corner of {deg} degrees, "
                    + "so the rule is not what this test says it is");
                ++nAcute;
            }
            else
            {
                Assert.True(inside,
                    $"the shipped waypoint is OUTSIDE at an obtuse corner of {deg} degrees");
                ++nObtuse;
            }
        }

        Assert.True(nAcute > 8 && nObtuse > 8);
    }


    /**
     * ...and at exactly 90 degrees it lands ON the kerb line, where inside and outside are
     * a rounding decision.
     *
     * This is why the whole-city inside/outside percentage is a poor description of the
     * defect on its own and why the signed distance is reported beside it: at the median
     * block corner the shipped waypoint is on the line to within a rounding error, and the
     * five obtuse-but-outside corners of Yelukhdidru/3000 are all between 90.000 and
     * 90.002 degrees.
     */
    [Fact]
    public void AtNinetyDegreesTheShippedWaypointLandsOnTheKerbItself()
    {
        var ring = _wedge(90f);

        Assert.True(_distToRing(ring, _asShipped(ring, 0)) < 1e-3f,
            "the shipped waypoint at a right angled corner is supposed to be on the kerb");
    }


    /**
     * The corner's own mitre is inside whatever the corner does.
     *
     * The same sweep, including the reflex half that a block folding inward produces - 6 to
     * 16 % of real block corners - and the near-degenerate ends where the mitre's own
     * length runs away.
     */
    [Theory]
    [InlineData(2f)]
    [InlineData(4f)]
    [InlineData(6f)]
    public void TheWalkIsInsideAtEveryCornerAngle(float width)
    {
        for (int deg = 5; deg <= 355; deg += 5)
        {
            var ring = _wedge(deg);
            var walk = PavementWalk.RingOf(ring, width);
            Assert.NotNull(walk);

            for (int i = 0; i < ring.Count; ++i)
            {
                Assert.True(SidewalkRing.ContainsInPlan(ring, walk[i]),
                    $"at {deg} degrees on a {width} m pavement, corner {i} of the walk is "
                    + "outside its own block");

                Assert.True((walk[i] - ring[i]).Length() <= width + 1e-3f,
                    $"at {deg} degrees the walk stands {(walk[i] - ring[i]).Length():F2} m "
                    + $"from its corner, off a {width} m pavement");
            }
        }
    }


    /**
     * A 1 m pavement does not hold a walker 1.5 m in - and no generated city can say so.
     *
     * Quarter.SidewalkWidth is 1 m where downtownness is below 0.2, and **not one block
     * centre of the four baseline cities is**: 0 of 2918 corners. So the narrowest pavement
     * the game can build is a shape unlimited real data cannot test, which is the same trap
     * §7o hit with the overlapping junction footprint - and the reason the offset is half
     * the pavement rather than a constant is exactly this case.
     */
    [Fact]
    public void ANarrowPavementHoldsTheWalkerOnIt()
    {
        Assert.Equal(0.5f, PavementWalk.OffsetFor(1f));
        Assert.Equal(1.0f, PavementWalk.OffsetFor(2f));
        Assert.Equal(1.5f, PavementWalk.OffsetFor(4f));
        Assert.Equal(1.5f, PavementWalk.OffsetFor(6f));

        var ring = _wedge(90f);
        var walk = PavementWalk.RingOf(ring, 1f);

        for (int i = 0; i < ring.Count; ++i)
        {
            Assert.True(_distToRing(ring, walk[i]) <= 1f + 1e-3f,
                $"on a 1 m pavement the walk stands {_distToRing(ring, walk[i]):F2} m from "
                + "the kerb");
        }
    }


    /**
     * Between two corners the walk runs parallel to the kerb it follows.
     *
     * Both of a segment's ends are one offset from the SAME edge line, so the whole segment
     * is - which is the property that makes a per corner waypoint enough, and the reason the
     * mitre beats a point taken from the pavement's own inset ring even though that ring is
     * the drawn surface. Asserted at ten positions along every segment, not at its ends.
     */
    [Theory]
    [InlineData(35f)]
    [InlineData(90f)]
    [InlineData(140f)]
    public void TheWalkRunsParallelToTheKerbItFollows(float degrees)
    {
        var ring = _wedge(degrees);
        var walk = PavementWalk.RingOf(ring, 4f);
        float offset = PavementWalk.OffsetFor(4f);

        for (int i = 0; i < ring.Count; ++i)
        {
            Vector2 e0 = ring[i], e1 = ring[(i + 1) % ring.Count];
            Vector2 d = Vector2.Normalize(e1 - e0);

            for (int k = 0; k <= 10; ++k)
            {
                Vector2 p = Vector2.Lerp(walk[i], walk[(i + 1) % ring.Count], k / 10f);
                float perp = Single.Abs(d.X * (p.Y - e0.Y) - d.Y * (p.X - e0.X));

                Assert.True(perp <= offset + 1e-3f,
                    $"{degrees} degrees, edge {i} at t={k / 10f:F1}: the walk is {perp:F3} m "
                    + $"from its own kerb, against an offset of {offset:F3}");
                Assert.True(SidewalkRing.ContainsInPlan(ring, p));
            }
        }
    }


    /**
     * A block that folds inward is walked round the fold, not across it.
     *
     * This is what rules out the pavement's own inset ring as the walk. SidewalkRing's
     * points belong to EDGES and deliberately not to corners - that is its whole design -
     * and joining consecutive ones cuts across every corner. At a convex corner that is
     * harmless; at a reflex one the cut leaves the block. Measured on the real cities, one
     * point per corner puts 0.5 to 1.2 % of the path outside by up to 11.07 m and both
     * points of every edge 0.1 to 0.3 % by up to 6.20 m, against 0.0 % here - so the
     * fixture carries the cut alongside, and the choice stays a measurement.
     */
    [Fact]
    public void AReflexCornerIsWalkedRoundAndNotCutOff()
    {
        /*
         * A dart: the corner at (30, 45) folds deep into the block.
         */
        var ring = new List<Vector2>
        {
            new(0f, 0f), new(0f, 60f), new(60f, 60f), new(60f, 0f), new(30f, 45f)
        };
        if (SidewalkRing.SignedArea2Of(ring) > 0f) ring.Reverse();

        int iReflex = -1;
        for (int i = 0; i < ring.Count; ++i)
        {
            if (_interiorAngleAt(ring, i) > 180f) iReflex = i;
        }

        Assert.True(iReflex >= 0,
            "the fixture no longer has a reflex corner, so it tests nothing it was built "
            + "for");

        var walk = PavementWalk.RingOf(ring, 4f);
        for (int i = 0; i < ring.Count; ++i)
        {
            for (int k = 0; k <= 20; ++k)
            {
                Vector2 p = Vector2.Lerp(walk[i], walk[(i + 1) % ring.Count], k / 20f);
                Assert.True(SidewalkRing.ContainsInPlan(ring, p),
                    $"the walk leaves the block on the segment from corner {i}");
            }
        }

        /*
         * ...and the pavement's own inset ring, joined up, does not - which is the
         * measurement that decided this.
         */
        var outer = ring.Select(p => new Vector3(p.X, 0f, p.Y)).ToList();
        var inset = SidewalkRing.InsetOf(outer, 4f);
        Assert.NotNull(inset);

        var cut = new List<Vector2>();
        foreach (var e in inset)
        {
            cut.Add(new Vector2(e.Start.X, e.Start.Z));
            cut.Add(new Vector2(e.End.X, e.End.Z));
        }

        bool leaves = false;
        for (int i = 0; i < cut.Count; ++i)
        {
            for (int k = 0; k <= 20; ++k)
            {
                if (!SidewalkRing.ContainsInPlan(
                        ring, Vector2.Lerp(cut[i], cut[(i + 1) % cut.Count], k / 20f)))
                {
                    leaves = true;
                }
            }
        }

        Assert.True(leaves,
            "the inset ring no longer cuts this reflex corner, so the reason the walk does "
            + "not use it is no longer demonstrated here");
    }


    /**
     * Which side is inward comes from the ring, not from how blocks happen to be traced.
     *
     * All 659 blocks of the baseline cities are traced clockwise today, so a constant would
     * be right and would silently put every citizen in the city into the road the day the
     * tracing order changed - the same argument NavLane.KerbSide is built on. Fed the same
     * ring both ways round, the walk must land on the same side.
     */
    [Fact]
    public void ACounterclockwiseRingWalksInsideToo()
    {
        var cw = _wedge(70f);
        var ccw = new List<Vector2>(cw);
        ccw.Reverse();

        var a = PavementWalk.RingOf(cw, 4f);
        var b = PavementWalk.RingOf(ccw, 4f);

        foreach (var p in b)
        {
            Assert.True(SidewalkRing.ContainsInPlan(ccw, p),
                "the walk of a counterclockwise ring is outset, i.e. down the middle of the "
                + "road");
        }

        /*
         * The same corner, named from either end, gets the same point.
         */
        for (int i = 0; i < cw.Count; ++i)
        {
            int j = (ccw.Count - 1 - i + ccw.Count) % ccw.Count;
            Assert.Equal(cw[i], ccw[j]);
            Assert.True((a[i] - b[j]).Length() < 1e-3f,
                $"corner {i} walks {(a[i] - b[j]).Length():F3} m apart depending on which "
                + "way the ring was traced");
        }
    }


    /**
     * At a corner that does not turn, this IS the expression that shipped.
     *
     * The mitre of two parallel edges is one offset along their common normal, so the whole
     * construction reduces to the old one wherever the ring runs straight. Measured over the
     * real cities, 5 % of corners do not move at all for exactly this reason.
     */
    [Fact]
    public void AStraightCornerReproducesTheShippedExpression()
    {
        var ring = new List<Vector2>
        {
            new(0f, 0f), new(0f, 30f), new(0f, 60f), new(60f, 60f), new(60f, 0f)
        };
        if (SidewalkRing.SignedArea2Of(ring) > 0f) ring.Reverse();

        int straight = -1;
        for (int i = 0; i < ring.Count; ++i)
        {
            if (Single.Abs(_interiorAngleAt(ring, i) - 180f) < 1e-2f) straight = i;
        }

        Assert.True(straight >= 0, "the fixture has no straight corner any more");

        var walk = PavementWalk.RingOf(ring, 3f);

        Assert.True((walk[straight] - _asShipped(ring, straight)).Length() < 1e-4f,
            "a corner that does not turn is supposed to be left exactly where it was");
    }


    /**
     * A block with nowhere to stand keeps the kerb, and a ring with no inside gets nothing.
     *
     * QuarterGenerator traces whatever the street graph leaves, including slivers, and a
     * block narrower than its own pavement has no position off the kerb that is not across
     * it. The kerb line is the honest answer there - it is where the pavement is, and it is
     * never in the road - and it is reached by CHECKING the constructed point rather than by
     * arguing that the construction cannot fail.
     */
    [Fact]
    public void ABlockWithNowhereToStandKeepsItsKerb()
    {
        var sliver = new List<Vector2>
        {
            new(0f, 0f), new(0f, 0.2f), new(80f, 0.2f), new(80f, 0f)
        };
        if (SidewalkRing.SignedArea2Of(sliver) > 0f) sliver.Reverse();

        var walk = PavementWalk.RingOf(sliver, 6f);
        Assert.NotNull(walk);
        Assert.Equal(sliver.Count, walk.Count);

        for (int i = 0; i < sliver.Count; ++i)
        {
            Assert.True(walk[i] == sliver[i] || SidewalkRing.ContainsInPlan(sliver, walk[i]),
                $"corner {i} of a sliver block walks outside it instead of falling back to "
                + "the kerb");
        }

        Assert.True(walk.Where((p, i) => p == sliver[i]).Any(),
            "a block 0.2 m across is supposed to defeat a 6 m pavement");

        Assert.Null(PavementWalk.RingOf(
            new List<Vector2> { new(0f, 0f), new(10f, 0f) }, 2f));
        Assert.Null(PavementWalk.RingOf(
            new List<Vector2> { new(0f, 0f), new(10f, 0f), new(20f, 0f) }, 2f));
        Assert.Null(PavementWalk.RingOf(_wedge(90f), 0f));
        Assert.Null(PavementWalk.RingOf(null, 2f));
    }


    /**
     * A repeated corner has no direction and is left on the kerb rather than guessed at.
     *
     * Asserted as EQUALITY with the corner, not as "inside somewhere": with an arbitrary
     * direction substituted for the missing one the point still lands inside a big convex
     * block most of the time, so a containment test cannot tell a guess from a refusal.
     */
    [Fact]
    public void ARepeatedCornerIsLeftAlone()
    {
        var ring = new List<Vector2>
        {
            new(0f, 0f), new(0f, 60f), new(0f, 60f), new(60f, 60f), new(60f, 0f)
        };
        if (SidewalkRing.SignedArea2Of(ring) > 0f) ring.Reverse();

        int iZero = -1;
        for (int i = 0; i < ring.Count; ++i)
        {
            if ((ring[(i + 1) % ring.Count] - ring[i]).Length() < 1e-4f) iZero = i;
        }

        Assert.True(iZero >= 0, "the fixture no longer has a zero length edge");

        var walk = PavementWalk.RingOf(ring, 4f);
        Assert.NotNull(walk);

        Assert.Equal(ring[iZero], walk[iZero]);
        Assert.Equal(ring[(iZero + 1) % ring.Count], walk[(iZero + 1) % ring.Count]);

        for (int i = 0; i < ring.Count; ++i)
        {
            Assert.True(walk[i] == ring[i] || SidewalkRing.ContainsInPlan(ring, walk[i]));
        }
    }


    /**
     * A corner that doubles back on itself has no mitre at any finite distance.
     *
     * The mitre's denominator is 1 + cos(180 - t), which is zero at t = 0 - a spike, where
     * the two edges lie on top of each other and there is no point one offset from both of
     * them on the inward side. SidewalkRing.MitreOf says so rather than dividing.
     */
    [Fact]
    public void ASpikeHasNoMitre()
    {
        Vector2 d = new(1f, 0f);
        Vector2 n = SidewalkRing.InwardNormalOf(d, false);

        Assert.False(SidewalkRing.MitreOf(n, -n, 1.5f, out _),
            "a corner that reverses on itself is supposed to have no mitre");
        Assert.True(SidewalkRing.MitreOf(n, n, 1.5f, out Vector2 m));
        Assert.True((m - 1.5f * n).Length() < 1e-5f,
            "the mitre of two parallel edges is one offset along their common normal");
    }
}
