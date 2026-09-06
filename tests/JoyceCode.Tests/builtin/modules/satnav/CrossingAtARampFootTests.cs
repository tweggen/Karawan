using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * WP-B6 triage item 1 — the pedestrian crossings at a junction a ramp leaves.
 *
 * ⚠️ THE BRIEF'S DEFECT DOES NOT HAPPEN, AND THE ONE THAT DOES IS ITS OPPOSITE.
 *
 * The brief judged "GenerateNavMapOperator draws a pedestrian crossing pairing an ordinary
 * arm with a ramp, which puts walkers on a structure" a ship blocker. Measured on the
 * shipped terrain with the flag on, before anything was changed: the operator ATTEMPTED
 * 33 such crossings across the four seeds that carry structures and EMITTED none of them,
 * because WP-B5 moved the pavement corner off the section-array mitre, so the lookup that
 * turns a section point into a pavement corner misses and the crossing is dropped.
 *
 * The same miss dropped the ordinary crossings at those junctions too: 24 of 103 were
 * emitted, against 55 % of every other crossing in the same cities. So a junction with a
 * ramp on it lost most of its pedestrian crossings - silently, on a `continue` - which is
 * a walker who cannot cross the road rather than a walker on a bridge.
 *
 * The fix is one rule, not two: the crossing loop walks the BLOCK arms and takes each
 * corner from StreetPoint.SectionPointBetween, which is the expression QuarterGenerator
 * files the pavement corner from (§14.3). A structure is not a block arm, so it cannot be
 * crossed and cannot be measured from; the ordinary arms either side of it are adjacent in
 * that ring and their mitre IS the block's corner. After: 42 of 75, i.e. the city's own
 * rate, and 0 attempted across a structure.
 */
public class CrossingAtARampFootTests
{
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000", 500f },
        new object[] { "seed011", 500f },
        new object[] { "Yelukhdidru", 400f },
        new object[] { "Yelukhdidru", 800f },
        new object[] { "seed000", 1500f },
        new object[] { "seed017", 2400f },
        new object[] { "Yelukhdidru", 3000f },
    };


    /**
     * The seeds that carry a structure, with the number of crossings emitted at the
     * junctions that carry a structure arm, and the number of block arms there.
     *
     * ⚠️ These are the numbers the fix is: before it they were 2 of 14, 3 of 14, 4 of 18
     * and 15 of 57, with a further 4, 5, 7 and 17 crossings attempted straight across a
     * ramp. The arm counts fall too, and correctly: a junction with three arms one of
     * which is a ramp has TWO block arms, which is a bend and gets no crossing, exactly as
     * any other two-armed junction does.
     */
    public static IEnumerable<object[]> SeedsWithStructures => new List<object[]>
    {
        //                                   blockArms  crossingsEmittedThere
        new object[] { "Yelukhdidru", 800f,     12,       2 },
        new object[] { "seed000",     1500f,     8,       4 },
        new object[] { "seed017",     2400f,     8,       8 },
        new object[] { "Yelukhdidru", 3000f,    47,      28 },
    };


    private sealed class City
    {
        internal ClusterDesc Cluster;
        internal StrokeStore Strokes;
        internal QuarterStore Quarters;
        internal NavClusterContent Content;

        /** Every pavement corner of the city, on the operator's own tenth-metre key. */
        internal HashSet<(int, int)> Corners = new();
    }


    private static City _city(string idString, float size, bool gradeSeparation)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        StrokeStore store;
        if (gradeSeparation)
        {
            var (s, _) = StreetHarness.GenerateHeavyFirstReporting(
                idString, size,
                sp => ShippedTerrain.HeightAt(
                    cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            store = s;
        }
        else
        {
            store = StreetHarness.Generate(idString, size);
        }

        cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);
        var quarters = StreetHarness.GenerateQuarters(cd, store, idString);

        var city = new City
        {
            Cluster = cd,
            Strokes = store,
            Quarters = quarters,
            Content = GenerateNavMapOperator.ContentOf(cd, store, quarters, new NavCluster())
        };

        foreach (var q in quarters.GetQuarters())
        {
            if (q.IsInvalid()) continue;
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;
            foreach (var d in delims)
            {
                city.Corners.Add(((int)(d.StartPoint.X * 10), (int)(d.StartPoint.Y * 10)));
            }
        }

        return city;
    }


    /**
     * The pedestrian lanes that are CROSSINGS rather than pavement edges.
     *
     * Read off the geometry rather than off the operator: a crossing spans two pavement
     * corners of ONE junction, while a pavement edge runs between corners of two different
     * junctions - consecutive corners of a block always stand on different junctions,
     * because a block turns at each of them. So "both ends are corners of the same
     * junction" separates the two without the operator being asked which it built.
     */
    private static List<(NavLane Lane, StreetPoint At)> _crossingsOf(City city)
    {
        var byJunction = new Dictionary<StreetPoint, HashSet<(int, int)>>();
        foreach (var sp in city.Strokes.GetStreetPoints())
        {
            var arms = GenerateNavMapOperator.BlockArmsOf(sp);
            if (arms.Count < 2) continue;

            var set = new HashSet<(int, int)>();
            for (int i = 0; i < arms.Count; ++i)
            {
                Vector2 c = sp.SectionPointBetween(arms[i], arms[(i + 1) % arms.Count]);
                set.Add(((int)(c.X * 10), (int)(c.Y * 10)));
            }

            byJunction[sp] = set;
        }

        var crossings = new List<(NavLane, StreetPoint)>();
        foreach (var nl in city.Content.Lanes)
        {
            if (!nl.AllowedTypes.HasFlag(TransportationType.Pedestrian)) continue;

            var a = ((int)((nl.Start.Position.X - city.Cluster.Pos.X) * 10),
                     (int)((nl.Start.Position.Z - city.Cluster.Pos.Z) * 10));
            var b = ((int)((nl.End.Position.X - city.Cluster.Pos.X) * 10),
                     (int)((nl.End.Position.Z - city.Cluster.Pos.Z) * 10));

            foreach (var (sp, set) in byJunction)
            {
                if (set.Contains(a) && set.Contains(b))
                {
                    crossings.Add((nl, sp));
                    break;
                }
            }
        }

        return crossings;
    }


    /*
     * ================================================== which arms may be crossed ====
     */

    /**
     * Flag off, every arm of every junction is a block arm - so the crossing loop walks
     * exactly the ring it always walked, in the same order.
     *
     * Asserted as a sequence over whole generated cities: a filter that kept the right
     * arms in the wrong order would pair each arm with the wrong neighbours and move every
     * crossing in the city, and no count could see that.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryArmOfAFlagOffCityIsCrossable(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);

        int n = 0;
        foreach (var sp in store.GetStreetPoints())
        {
            Assert.Equal(sp.GetAngleArray(), GenerateNavMapOperator.BlockArmsOf(sp));
            n += sp.GetAngleArray().Count;
        }

        Assert.True(n > 0);
    }


    /**
     * Flag on, a structure arm is not crossable and every other arm still is.
     */
    [Theory]
    [MemberData(nameof(SeedsWithStructures))]
    public void ARampIsNotAnArmAWalkerMayCross(
        string idString, float size, int blockArmsAtFeet, int _)
    {
        var city = _city(idString, size, gradeSeparation: true);

        int nShorter = 0;
        foreach (var sp in city.Strokes.GetStreetPoints())
        {
            var arms = GenerateNavMapOperator.BlockArmsOf(sp);

            Assert.Equal(
                sp.GetAngleArray().Where(BlockGraph.IsBlockEdge).ToList(), arms);
            Assert.DoesNotContain(arms, s => StrokeKinds.IsStructure(s.Kind));

            if (arms.Count < sp.GetAngleArray().Count) ++nShorter;
        }

        Assert.True(nShorter > 0,
            $"{idString}@{size} places structures, so some junction must carry one");
    }


    /*
     * ================================================== what comes out ===============
     */

    /**
     * ⚠️ THE GATE THIS WORK PACKAGE IS: a junction a ramp leaves still gets its
     * pedestrian crossings.
     *
     * Counted at the junctions that carry a structure arm, off the operator's own emitted
     * lanes. The control is the rest of the same city: if the fix had merely stopped
     * attempting the impossible crossings without recovering the possible ones, this would
     * still be zero.
     */
    [Theory]
    [MemberData(nameof(SeedsWithStructures))]
    public void ARampFootStillGetsItsPedestrianCrossings(
        string idString, float size, int blockArmsAtFeet, int expectedCrossingsThere)
    {
        var city = _city(idString, size, gradeSeparation: true);
        var crossings = _crossingsOf(city);

        var feet = city.Strokes.GetStreetPoints()
            .Where(sp => sp.GetAngleArray().Any(s => StrokeKinds.IsStructure(s.Kind))
                         && GenerateNavMapOperator.BlockArmsOf(sp).Count >= 3)
            .ToList();

        int arms = feet.Sum(sp => GenerateNavMapOperator.BlockArmsOf(sp).Count);
        Assert.Equal(blockArmsAtFeet, arms);

        var cornersOfFeet = new HashSet<(int, int)>();
        foreach (var sp in feet)
        {
            var blockArms = GenerateNavMapOperator.BlockArmsOf(sp);
            for (int i = 0; i < blockArms.Count; ++i)
            {
                Vector2 c = sp.SectionPointBetween(
                    blockArms[i], blockArms[(i + 1) % blockArms.Count]);
                cornersOfFeet.Add(((int)(c.X * 10), (int)(c.Y * 10)));
            }
        }

        /*
         * A crossing lane is emitted in both directions, so pairs are what the numbers
         * above count.
         */
        int lanesThere = crossings.Select(c => c.Lane).Count(nl =>
            cornersOfFeet.Contains(
                ((int)((nl.Start.Position.X - city.Cluster.Pos.X) * 10),
                 (int)((nl.Start.Position.Z - city.Cluster.Pos.Z) * 10)))
            && cornersOfFeet.Contains(
                ((int)((nl.End.Position.X - city.Cluster.Pos.X) * 10),
                 (int)((nl.End.Position.Z - city.Cluster.Pos.Z) * 10))));

        Assert.Equal(expectedCrossingsThere * 2, lanesThere);
    }


    /**
     * ⚠️ RECORDED AND NOT FIXED: a crossing at a ramp foot passes over the ramp's mouth.
     *
     * The brief's ship blocker was "a crossing pairing an ordinary arm with a ramp". That
     * one is gone - no crossing is measured from a structure arm any more, and none spans
     * one. What is left is a consequence of the block geometry WP-B5 settled and not of
     * this operator: the pavement corner at a foot is the mitre of the two BLOCK arms, so
     * the pavement itself cuts straight across the ramp mouth, and the crossing between two
     * such corners crosses the ramp's centre line with it.
     *
     * It is the pavement that is on the ramp, not the crossing that put it there; moving it
     * means moving the block corner, which is WP-B5's rule and every flag-on baseline.
     * Counted here so it is visible, with how high the ramp stands where it happens - a
     * crossing near a foot passes it at ground level, which is what a real interchange
     * does, and the number says whether it stays that way. Measured: the worst of the
     * sixteen meets its ramp 1.26 m above the foot, i.e. a kerb and a bit, on a ramp that
     * needs 80 m to reach its deck.
     */
    [Theory]
    [InlineData("Yelukhdidru", 800f, 0, 0.0f)]
    [InlineData("seed000", 1500f, 2, 1.05f)]
    [InlineData("seed017", 2400f, 4, 0.95f)]
    [InlineData("Yelukhdidru", 3000f, 14, 1.27f)]
    public void ACrossingAtAFootPassesTheRampMouthAtGroundLevel(
        string idString, float size, int expectedLanes, float worstRampRise)
    {
        var city = _city(idString, size, gradeSeparation: true);
        var crossings = _crossingsOf(city);

        int n = 0;
        float worst = 0f;

        foreach (var (nl, sp) in crossings)
        {
            Vector2 a = new(nl.Start.Position.X - city.Cluster.Pos.X,
                            nl.Start.Position.Z - city.Cluster.Pos.Z);
            Vector2 b = new(nl.End.Position.X - city.Cluster.Pos.X,
                            nl.End.Position.Z - city.Cluster.Pos.Z);

            foreach (var s in sp.GetAngleArray())
            {
                if (!StrokeKinds.IsStructure(s.Kind)) continue;
                if (!_crosses(a, b, s.A.Pos, s.B.Pos)) continue;

                ++n;

                /*
                 * How far the ramp has climbed where the crossing meets it - its own
                 * MaxRampGrade over the distance from the foot, which is the only thing
                 * that decides whether this is a level crossing or a wall.
                 */
                Vector2 x = _intersectionOf(a, b, s.A.Pos, s.B.Pos);
                float fromFoot = (x - sp.Pos).Length();
                worst = Single.Max(worst, fromFoot * new GradePolicy().MaxRampGrade);
            }
        }

        Assert.Equal(expectedLanes, n);
        Assert.True(worst <= worstRampRise + 0.01f,
            $"{idString}@{size}: a crossing meets a ramp {worst:F2} m above its foot");
    }


    private static Vector2 _intersectionOf(
        in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d)
    {
        Vector2 r = b - a, s2 = d - c;
        float denom = r.X * s2.Y - r.Y * s2.X;
        float t = ((c.X - a.X) * s2.Y - (c.Y - a.Y) * s2.X) / denom;

        return a + r * t;
    }


    /**
     * Do the two open segments cross, strictly?
     */
    private static bool _crosses(in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d)
    {
        float d1 = _side(c, d, a), d2 = _side(c, d, b);
        float d3 = _side(a, b, c), d4 = _side(a, b, d);

        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
               && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }


    private static float _side(in Vector2 p, in Vector2 q, in Vector2 r)
        => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);


    /**
     * Flag off, the city gets exactly the crossings it always got.
     *
     * The two rules agree by construction on a city with no structure in it - the section
     * array is filled FROM SectionPointBetween, so where two arms are adjacent the mitre
     * is the same float the section map holds - and this is that claim measured over whole
     * generated cities rather than argued from the code.
     */
    [Theory]
    [InlineData("seed000", 500f, 8)]
    [InlineData("seed011", 500f, 0)]
    [InlineData("Yelukhdidru", 400f, 0)]
    [InlineData("Yelukhdidru", 800f, 28)]
    [InlineData("seed000", 1500f, 444)]
    [InlineData("seed017", 2400f, 1398)]
    [InlineData("Yelukhdidru", 3000f, 2822)]
    public void AFlagOffCityGetsExactlyTheCrossingLanesItAlwaysGot(
        string idString, float size, int expectedCrossingLanes)
    {
        var city = _city(idString, size, gradeSeparation: false);

        Assert.Equal(expectedCrossingLanes, _crossingsOf(city).Count);
    }
}
