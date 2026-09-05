using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B3b — placement. Where structures actually appear.
 *
 * Two kinds of test, and both are needed for the same reason WP-B1 and WP-B3a needed
 * both: **no city the shipped ruleset builds contained a structure until this work
 * package**, so real data could not catch a placement bug on its own, and a fixture on
 * its own cannot say whether the rule ever fires. So:
 *
 * - whole generated cities, on the shipped terrain, for the counts, the grades, the
 *   clearance distribution and the one property that ties the placement to the game -
 *   that relaxing the FINISHED network reproduces exactly the grades the placer refused
 *   on;
 * - fixtures for every refusal, because five of the nine are reached by no generated
 *   city and one of them by no builder in the tree at all.
 */
public class StructurePlacementTests
{
    /**
     * The seeds StreetDeterminismTests pins, minus Yelukhdidru@100 which generates
     * nothing at all. @400 stays in: refusing a whole city is a legitimate outcome and
     * the point is that it is VISIBLE.
     */
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
     * What each seed does on the shipped terrain: crossings looked at, structures
     * placed, and how many of the refusals were the deck's own grade.
     *
     * Recorded here rather than in a baseline file because these are the answer this
     * work package exists to produce and they should be read, not diffed.
     */
    public static IEnumerable<object[]> TerrainYield => new List<object[]>
    {
        //                    considered  placed  refusedForDeckGrade
        new object[] { "seed000",     500f,   10,  0,  1 },
        new object[] { "seed011",     500f,    9,  0,  1 },
        new object[] { "Yelukhdidru", 400f,    1,  0,  0 },
        new object[] { "Yelukhdidru", 800f,   26,  2,  4 },
        new object[] { "seed000",     1500f, 140,  1, 20 },
        new object[] { "seed017",     2400f, 366,  6, 43 },
        new object[] { "Yelukhdidru", 3000f, 549,  9, 75 },
    };


    private static (StrokeStore Store, ClusterDesc Cluster, StructurePlacementReport Report)
        _onTerrain(string idString, float size)
    {
        var cluster = StreetHarness.MakeCluster(idString, size);

        var (store, generator) = StreetHarness.GenerateHeavyFirstReporting(
            idString, size,
            sp => ShippedTerrain.HeightAt(
                cluster.Pos.X + sp.Pos.X, cluster.Pos.Z + sp.Pos.Y));

        return (store, cluster, generator.StructurePlacement);
    }


    /**
     * The heights the GAME will answer for this network - RelaxedStreetHeight's own two
     * steps over the finished store, structures and all.
     */
    private static Dictionary<int, float> _finalHeights(ClusterDesc cluster, StrokeStore store)
    {
        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = ShippedTerrain.HeightAt(
                cluster.Pos.X + sp.Pos.X, cluster.Pos.Z + sp.Pos.Y);
        }

        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());
        return heights;
    }


    private static float _roadGradeOf(Stroke s, Dictionary<int, float> heights)
        => ((heights[s.B.Id] + s.B.LevelElevation) - (heights[s.A.Id] + s.A.LevelElevation))
           / s.Length;


    /*
     * ============================================================ the yield ==========
     */

    /**
     * ⚠️ THE HEADLINE: how many structures a city gets, and how many corridors were
     * refused.
     *
     * Nine on the largest city, six on the next, one, two, and none at all on the three
     * small ones - out of 549, 366, 140, 26, 10, 9 and 1 crossings looked at. That is
     * D2's "a handful of correct structures per city" measured rather than hoped for.
     */
    [Theory]
    [MemberData(nameof(TerrainYield))]
    public void ACityGetsTheStructuresItsOwnCorridorsPermit(
        string idString, float size, int considered, int placed, int deckGradeRefusals)
    {
        var (store, _, report) = _onTerrain(idString, size);

        Assert.Equal(considered, report.Considered);
        Assert.Equal(placed, report.Placed);

        report.Refused.TryGetValue(StructureRefusal.DeckGrade, out int refusedForGrade);
        Assert.Equal(deckGradeRefusals, refusedForGrade);

        Assert.Equal(placed, store.GetStrokes().Count(s => s.Kind == StrokeKind.Bridge));
        Assert.Equal(2 * placed, store.GetStrokes().Count(s => s.Kind == StrokeKind.Ramp));
    }


    /**
     * ⚠️ The refusal the plan did not have, and it is the one that does the work.
     *
     * §2's corridor-fit table asks whether two ramps FIT and predicted 15 buildable
     * crossings in the largest city. Geometry alone still admits that many - the flat
     * city below places 88 - and it is the DECK's grade that takes the terrain city down
     * to nine. Over these seeds the deck's own grade refuses 144 corridors against 3 for
     * the ramps' plan clearance and 4 for the deck's vertical one.
     */
    [Fact]
    public void TheDeckGradeIsWhatRefusesMostCorridors()
    {
        int deck = 0, other = 0;

        foreach (var row in TerrainYield)
        {
            var (_, _, report) = _onTerrain((string)row[0], (float)row[1]);

            foreach (var kv in report.Refused)
            {
                if (kv.Key == StructureRefusal.DeckGrade) deck += kv.Value;
                else if (kv.Key == StructureRefusal.DeckClearance
                         || kv.Key == StructureRefusal.RampClearance) other += kv.Value;
            }
        }

        Assert.Equal(144, deck);
        Assert.Equal(7, other);
    }


    /**
     * ⚠️ AND THE PROPERTY THAT MAKES THE REFUSAL MEAN ANYTHING: the grades the placer
     * judged are the grades the game computes.
     *
     * §3b says there is no relaxed height at generation time, which is true of
     * RelaxedStreetHeight. What the placer computes instead is GradeRelaxer's own ANCHOR
     * pass over the ground strokes the finished network will have - so this is not an
     * estimate that happens to agree. Relaxing the finished store from the same terrain
     * gives every ramp exactly MaxRampGrade and every deck a grade inside its own bound,
     * asserted to five decimals on cities that were never told what answer to give.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheFinishedCityCarriesExactlyTheGradesThePlacerJudged(string idString, float size)
    {
        var (store, cluster, report) = _onTerrain(idString, size);
        if (0 == report.Placed)
        {
            return;
        }

        var heights = _finalHeights(cluster, store);
        var policy = new GradePolicy();

        foreach (var ramp in store.GetStrokes().Where(s => s.Kind == StrokeKind.Ramp))
        {
            Assert.Equal(policy.MaxRampGrade, Single.Abs(_roadGradeOf(ramp, heights)), 5);
        }

        var decks = store.GetStrokes()
            .Where(s => s.Kind == StrokeKind.Bridge)
            .Select(s => _roadGradeOf(s, heights))
            .OrderBy(g => g)
            .ToList();

        Assert.Equal(
            report.DeckGrades.OrderBy(g => g).Select(g => MathF.Round(g, 5)),
            decks.Select(g => MathF.Round(g, 5)));

        foreach (var deck in store.GetStrokes().Where(s => s.Kind == StrokeKind.Bridge))
        {
            Assert.True(
                Single.Abs(_roadGradeOf(deck, heights)) <= policy.MaxDeckGradeFor(deck),
                $"{idString}@{size}: a deck came out at "
                + $"{_roadGradeOf(deck, heights):P2} against its bound of "
                + $"{policy.MaxDeckGradeFor(deck):P2}");
        }
    }


    /**
     * B3.5 - the clearance report, whose distribution is the answer to whether the fixed
     * `level * DeckHeight` deck model needs decoupling.
     *
     * It does not, yet, and the number says why: over the twenty crossings a deck flies
     * over on the shipped terrain the clearance runs **4.53 to 20.30 m** against a
     * nominal 8, i.e. 0.57x to 2.5x. The spread is entirely the ground: the deck is
     * pinned 8 m over the ground at its own ENDS, and what passes underneath is at
     * whatever height the ground is in between. Nothing here is under
     * MinDeckClearance, because a corridor whose crossing would be is refused - four of
     * them, on Yelukhdidru@3000.
     */
    [Fact]
    public void TheClearanceUnderADeckIsReportedAndIsNeverLessThanTrafficNeeds()
    {
        var all = new List<float>();

        foreach (var row in TerrainYield)
        {
            var (store, cluster, report) = _onTerrain((string)row[0], (float)row[1]);
            all.AddRange(report.Clearances);

            if (0 == report.Placed) continue;

            /*
             * ...and measured again on the finished network rather than trusted from the
             * report, because the report is the thing under test.
             */
            var heights = _finalHeights(cluster, store);

            foreach (var deck in store.GetStrokes().Where(s => s.Kind == StrokeKind.Bridge))
            {
                foreach (var under in store.GetStrokes())
                {
                    if (under.Level != 0 || StrokeKinds.IsStructure(under.Kind)) continue;

                    var si = deck.Intersects(under);
                    if (null == si) continue;

                    float onDeck = _at(deck, si.ScaleExists, heights);
                    float onGround = _at(under, si.ScaleCand, heights);

                    Assert.True(onDeck - onGround >= StructurePlacer.MinDeckClearance,
                        $"a deck stands {onDeck - onGround:F2} m over the road it crosses");
                }
            }
        }

        Assert.Equal(20, all.Count);
        Assert.Equal(4.53f, all.Min(), 2);
        Assert.Equal(20.30f, all.Max(), 2);

        /*
         * The nominal is one deck height, and the ground under a crossing is very rarely
         * the ground under the feet - which is exactly why this is measured.
         */
        Assert.True(all.Count(c => Single.Abs(c - 8f) > 1f) > 10,
            "if nearly every clearance were the nominal 8 m this measurement would be "
            + "reporting the constant back rather than the terrain");
    }


    private static float _at(Stroke s, float t, Dictionary<int, float> heights)
    {
        float a = heights[s.A.Id] + s.A.LevelElevation;
        float b = heights[s.B.Id] + s.B.LevelElevation;
        return a + t * (b - a);
    }


    /**
     * Lifting a corridor does not take the city apart.
     *
     * A lift removes two edges and adds a path between their far ends, so a junction that
     * reached the rest of the network only through one of them could in principle be cut
     * off. Measured: every one of these cities is still a single component, flat and on
     * terrain, with up to 88 structures in it.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ACityWithStructuresInItIsStillOnePiece(string idString, float size)
    {
        Assert.Equal(1, StreetHarness.CountComponents(_onTerrain(idString, size).Store));
        Assert.Equal(1, StreetHarness.CountComponents(
            StreetHarness.GenerateHeavyFirst(idString, size)));
    }


    /**
     * ...and the blocks still trace. §3c says what they LOOK like is WP-B5's, and it is;
     * this is only that QuarterGenerator survives a network with a deck in it.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheBlocksOfACityWithStructuresStillTrace(string idString, float size)
    {
        var (store, cluster, _) = _onTerrain(idString, size);

        int withStructures = StreetHarness
            .GenerateQuarters(cluster, store, idString).GetQuarters().Count;
        int flagOff = StreetHarness
            .GenerateQuarters(cluster, StreetHarness.Generate(idString, size), idString)
            .GetQuarters().Count;

        Assert.Equal(flagOff > 0, withStructures > 0);
    }


    /**
     * The same seed twice gives the same structures. Nothing here draws from the
     * RandomSource, so this is about the ordering of the candidate walk and of the fixed
     * point, both of which are keyed on junction id.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void PlacementIsRepeatableWithinAProcess(string idString, float size)
    {
        var first = _onTerrain(idString, size);
        var second = _onTerrain(idString, size);

        Assert.Equal(first.Report.Placed, second.Report.Placed);
        Assert.Equal(first.Report.Considered, second.Report.Considered);
        Assert.Equal(
            StreetNetworkFingerprint.CanonicalLinesV2(first.Store),
            StreetNetworkFingerprint.CanonicalLinesV2(second.Store));
    }


    /**
     * ⚠️ Flat ground and terrain are two different worlds for this policy, and saying so
     * is the point.
     *
     * The two flags are independent (§5), so a flat city may have overpasses. On level
     * ground every deck grade is zero, so nothing is refused for it and only the geometry
     * rules bind - which admits **88** structures in Yelukhdidru@3000 against nine on the
     * terrain. That is the clearest statement available of what the deck-grade bound is
     * doing, and it is also why the recorded flag-on fingerprint is a flat city: it gates
     * the geometry with nothing else in the way.
     */
    [Fact]
    public void OnLevelGroundOnlyTheGeometryRefusesAnything()
    {
        var (_, generator) = StreetHarness.GenerateHeavyFirstReporting("Yelukhdidru", 3000f, null);
        var report = generator.StructurePlacement;

        Assert.Equal(88, report.Placed);
        Assert.False(report.Refused.ContainsKey(StructureRefusal.DeckGrade));
        Assert.False(report.Refused.ContainsKey(StructureRefusal.DeckClearance));

        foreach (float grade in report.DeckGrades)
        {
            Assert.Equal(0f, grade, 3);
        }
    }


    /*
     * ============================================================ visible refusal ====
     */

    /**
     * ⚠️ "No bridges appeared" and "the policy never ran" have to be different
     * observations, and the AC is that the difference is VISIBLE.
     *
     * A Warning, every run, whether or not anything was placed - so this is asserted on
     * the log, which is where the property lives. A source scan would be satisfied by the
     * identifier surviving in a comment (§6), and a test on the report object would pass
     * with the Warning deleted.
     */
    [Fact]
    public void ACityThatGetsNoStructureSaysSoInTheLog()
    {
        using var log = new LogCapture();

        _onTerrain("Yelukhdidru", 400f);

        Assert.True(log.Saw("grade separation:"),
            "the placement pass said nothing at all about a city it refused entirely");
        Assert.True(log.Saw("0 structures placed"));
        Assert.True(log.Saw("InteriorTBranch"),
            "the reason a corridor was refused is not in the log");
    }


    /**
     * ...and a city that DOES get structures says that too, so the line is not a failure
     * report that only appears when something is wrong.
     */
    [Fact]
    public void ACityThatGetsStructuresSaysSoToo()
    {
        using var log = new LogCapture();

        _onTerrain("Yelukhdidru", 800f);

        Assert.True(log.Saw("2 structures placed"));
        Assert.True(log.Saw("clearance"));
    }


    /**
     * With the flag off nothing is looked at, nothing is placed, and nothing is said -
     * the whole pass is one branch on a property the shipped game never sets.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AFlagOffCityHasNoPlacementPassAtAll(string idString, float size)
    {
        using var log = new LogCapture();

        var cluster = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);

        Assert.Empty(store.GetStrokes().Where(s => StrokeKinds.IsStructure(s.Kind)));
        Assert.False(log.Saw($"Cluster {cluster.Name}: grade separation"));
    }


    /**
     * The pass refuses to guess when it has no ground to judge on, and says so.
     *
     * Silently placing nothing is the failure mode; silently placing everything is worse.
     */
    [Fact]
    public void WithNoGroundHeightNothingIsPlacedAndTheReasonIsInTheLog()
    {
        using var log = new LogCapture();

        var f = _cross();
        var report = StructurePlacer.Place(
            f.Store, 0, new GradePolicy(), null, 19.7f, 10f, 0f);

        Assert.Equal(0, report.Placed);
        Assert.True(log.Saw("no ground height source was supplied"));
    }


    /*
     * ============================================================ the fixtures =======
     */

    private const float ClusterSize = 4000f;

    /**
     * Weight of a fixture's ring roads - the lightest the ruleset builds, so the
     * relaxation's limit for one is 14 % and nothing in these fixtures reaches it.
     */
    private const float RingWeight = 0.2f;


    private sealed class Cross
    {
        internal StrokeStore Store;
        internal StreetPoint West, East, North, South, Middle;
        internal Dictionary<int, float> Ground = new();

        internal float GroundAt(StreetPoint sp)
            => Ground.TryGetValue(sp.Id, out float h) ? h : 0f;
    }


    private static StreetPoint _pointAt(float x, float y, sbyte level = 0)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _street(
        StrokeStore store, StreetPoint a, StreetPoint b, float weight,
        StrokeKind kind = StrokeKind.Street)
    {
        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = false, Weight = weight, Kind = kind, Level = 0
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);
        return s;
    }


    /**
     * The shape an overpass is: a four-arm crossing with a straight road through it.
     *
     * Both through arms are 200 m, comfortably over the 80 m a ramp needs plus the plan
     * separation it has to keep from the road underneath. The cross road is 150 m each
     * way so the deck, which reaches 120 m either side of the middle, unambiguously
     * passes over it.
     */
    private static Cross _cross(
        float throughArm = 200f, float crossArm = 150f, float weight = 1.0f,
        bool tBranch = false, float middleHeight = 0f)
    {
        var store = new StrokeStore(ClusterSize);

        var f = new Cross
        {
            Middle = _pointAt(0f, 0f),
            West = _pointAt(-throughArm, 0f),
            East = _pointAt(throughArm, 0f),
            North = _pointAt(0f, crossArm),
            South = _pointAt(0f, -crossArm)
        };

        f.Store = store;

        _street(store, f.West, f.Middle, weight);
        _street(store, f.Middle, f.East, weight);
        _street(store, f.Middle, f.North, weight);
        if (!tBranch)
        {
            _street(store, f.Middle, f.South, weight);
        }

        /*
         * ⚠️ The ring, and it is not decoration: a grade separated crossing has no slip
         * roads, so lifting the through road leaves the crossing road connected to it
         * nowhere. A real city closes over that - measured, every generated city stays in
         * one piece - and a four-stroke fixture does not, so without these the placer
         * would refuse this fixture for WouldDisconnect and every test below would be
         * measuring that instead.
         *
         * Light, so that the relaxation never corrects one and the fixture's heights are
         * the heights it was given. Two arms each, so they add no crossing of their own.
         */
        _street(store, f.West, f.North, RingWeight);
        _street(store, f.North, f.East, RingWeight);
        if (!tBranch)
        {
            _street(store, f.West, f.South, RingWeight);
            _street(store, f.South, f.East, RingWeight);
        }

        f.Ground[f.Middle.Id] = middleHeight;
        f.Ground[f.North.Id] = middleHeight;
        f.Ground[f.South.Id] = middleHeight;

        return f;
    }


    private static StructurePlacementReport _place(
        Cross f, float rampClearance = 19.7f, float minSpan = 10f, float maxSpan = 0f,
        GradePolicy policy = null)
        => StructurePlacer.Place(
            f.Store, 0, policy ?? new GradePolicy(), f.GroundAt,
            rampClearance, minSpan, maxSpan);


    /**
     * The positive control, and everything below is this fixture with one thing wrong.
     */
    [Fact]
    public void AFourArmCrossingWithALongStraightRoadThroughItIsLifted()
    {
        var f = _cross();

        var report = _place(f);

        Assert.Equal(1, report.Considered);
        Assert.Equal(1, report.Placed);
        Assert.Empty(report.Refused);

        /*
         * The road that was there is gone and the structure is in its place; the crossing
         * itself has not moved and keeps the road that now passes underneath.
         */
        Assert.Empty(f.Store.GetStrokes().Where(
            s => (s.A == f.Middle && (s.B == f.West || s.B == f.East))
                 || (s.B == f.Middle && (s.A == f.West || s.A == f.East))));

        Assert.Equal(2, f.Store.GetStrokes().Count(s => s.Kind == StrokeKind.Ramp));
        Assert.Single(f.Store.GetStrokes().Where(s => s.Kind == StrokeKind.Bridge));

        Assert.Equal(2, f.Middle.GetAngleArray().Count);
        Assert.Equal(Vector2.Zero, f.Middle.Pos);
    }


    /**
     * ⚠️ THE INTERIOR-T-BRANCH RULE, and the fixture is the control for it: the SAME
     * corridor, one arm fewer at the crossing.
     *
     * Identity rather than a metric: the two fixtures differ by exactly one stroke, and
     * the corridor, the feet and the geometry are the same objects in both.
     */
    [Fact]
    public void ACrossingThatWouldBeLeftWithOneArmIsNotLifted()
    {
        var lifted = _place(_cross(tBranch: false));
        var refused = _place(_cross(tBranch: true));

        Assert.Equal(1, lifted.Placed);

        Assert.Equal(0, refused.Placed);
        Assert.Equal(1, refused.Considered);
        Assert.Equal(1, refused.Refused[StructureRefusal.InteriorTBranch]);
    }


    /**
     * ...and what the exclusion costs, on the cities it is applied to: 277 of the 608
     * straight-through crossings of Yelukhdidru@3000 are three-arm, 166 of 334 on
     * seed017@2400, and Yelukhdidru@400's ONLY corridor is one - which is why that city
     * refuses everything.
     *
     * ⚠️ Most of those would have been refused anyway. Of the 204 three-arm crossings the
     * rule refuses in Yelukhdidru@3000, only 15 have arms long enough to hold two ramps
     * at all, so the exclusion's own cost is 15 corridors and not 204. Counted rather
     * than assumed, because "the rule costs 204" is the number the refusal tally shows
     * and it is the wrong one.
     */
    [Theory]
    [InlineData("Yelukhdidru", 3000f, 204, 85)]
    [InlineData("seed017", 2400f, 159, 71)]
    [InlineData("seed000", 1500f, 54, 23)]
    [InlineData("seed000", 500f, 4, 0)]
    public void TheTBranchExclusionCostsFarFewerCorridorsThanItRefuses(
        string idString, float size, int refusedForTBranch, int thatWouldHaveFitted)
    {
        var (store, _, report) = _onTerrain(idString, size);

        Assert.Equal(refusedForTBranch, report.Refused[StructureRefusal.InteriorTBranch]);

        var policy = new GradePolicy();
        float needed = OverpassBuilder.RampLengthFor(policy, 0, StrokeKind.Bridge)
                       + Stroke.WidthForWeight(1.3f);

        int fitted = 0;
        foreach (var m in store.GetStreetPoints())
        {
            var arms = m.GetAngleArray();
            if (null == arms || 3 != arms.Count) continue;

            var pair = _mostOpposedGroundPair(m, arms);
            if (null == pair) continue;

            if (pair.Value.Item1.Length >= needed && pair.Value.Item2.Length >= needed)
            {
                ++fitted;
            }
        }

        Assert.Equal(thatWouldHaveFitted, fitted);
    }


    private static (Stroke, Stroke)? _mostOpposedGroundPair(StreetPoint m, List<Stroke> arms)
    {
        Stroke bestA = null, bestB = null;
        float bestDot = StructurePlacer.MinStraightDot;

        for (int i = 0; i < arms.Count; ++i)
        for (int j = i + 1; j < arms.Count; ++j)
        {
            Stroke a = arms[i], b = arms[j];
            if (a.Kind != StrokeKind.Street || b.Kind != StrokeKind.Street) continue;

            StreetPoint pa = a.A == m ? a.B : a.A;
            StreetPoint pb = b.A == m ? b.B : b.A;
            if (pa == pb) continue;

            float dot = Vector2.Dot(
                Vector2.Normalize(pa.Pos - m.Pos), Vector2.Normalize(pb.Pos - m.Pos));
            if (dot <= bestDot) { bestDot = dot; bestA = a; bestB = b; }
        }

        return null == bestA ? null : (bestA, bestB);
    }


    /**
     * An arm has to hold a whole ramp AND leave the deck standing clear of the junction
     * it flies over - the same plan separation ClearanceConstraint demands of every
     * street the generator grows, applied to the structure the generator did not grow.
     */
    [Theory]
    [InlineData(200f, 1, 0)]
    [InlineData(100f, 1, 0)]
    [InlineData(99f, 0, 1)]
    [InlineData(79f, 0, 1)]
    public void AnArmMustHoldARampAndTheDecksOverhang(float arm, int placed, int refused)
    {
        var report = _place(_cross(throughArm: arm));

        Assert.Equal(placed, report.Placed);
        report.Refused.TryGetValue(StructureRefusal.ArmTooShort, out int n);
        Assert.Equal(refused, n);
    }


    /**
     * The ramp length comes from MaxRampGrade and from nothing else - so a policy that
     * builds shallower ramps needs longer arms, and the SAME corridor is refused.
     */
    [Fact]
    public void ARampsLengthFollowsThePolicysGradeAndSoDoesWhatFits()
    {
        var policy = new GradePolicy();
        Assert.Equal(80f, OverpassBuilder.RampLengthFor(policy, 0, StrokeKind.Bridge), 3);

        var shallow = new GradePolicy { MaxRampGrade = 0.05f };
        Assert.Equal(160f, OverpassBuilder.RampLengthFor(shallow, 0, StrokeKind.Bridge), 3);

        /*
         * §2's headline, as a placement outcome: at five percent nothing is buildable.
         */
        Assert.Equal(1, _place(_cross(throughArm: 150f)).Placed);
        Assert.Equal(0, _place(_cross(throughArm: 150f), policy: shallow).Placed);

        /*
         * ...and it is the CLIMB divided by the grade, so a tunnel needs the same length
         * going the other way.
         */
        Assert.Equal(
            OverpassBuilder.RampLengthFor(policy, 0, StrokeKind.Bridge),
            OverpassBuilder.RampLengthFor(policy, 0, StrokeKind.Tunnel), 3);
    }


    /**
     * A deck that is shorter than the deck rule permits is refused. Unreachable from any
     * generated city - the shortest deck a real corridor produces is 30 m against a
     * derived minimum of 19.7 - so it is a fixture and says so.
     */
    [Fact]
    public void ADeckShorterThanTheSpanRuleAllowsIsRefused()
    {
        var report = _place(_cross(), minSpan: 500f);

        Assert.Equal(0, report.Placed);
        Assert.Equal(1, report.Refused[StructureRefusal.SpanLength]);

        Assert.Equal(1, _place(_cross(), minSpan: 10f, maxSpan: 500f).Placed);
        Assert.Equal(1, _place(_cross(), minSpan: 10f, maxSpan: 50f)
            .Refused[StructureRefusal.SpanLength]);
    }


    /**
     * ⚠️ The corridor bends and the structure does not.
     *
     * A straight chain between two feet that are 26 degrees apart around their crossing
     * can pass to one side of the road it was supposed to fly over. Refused, because a
     * bridge over nothing is a bridge that removed a crossing without replacing it.
     *
     * Unreachable from a generated city, and the reason is worth stating: on a straight
     * corridor the chord passes exactly THROUGH the crossing, so every remaining arm
     * touches the deck at its own end and the test can never fail. Only a bent one can.
     */
    [Fact]
    public void AChainThatMissesTheRoadItWasToFlyOverIsRefused()
    {
        var store = new StrokeStore(ClusterSize);

        var m = _pointAt(0f, 0f);
        var west = _pointAt(-200f, 0f);

        /* 200 m out at a dot of -0.95 against due west, i.e. an 18 degree bend */
        var east = _pointAt(190f, 62.4f);

        /* both cross arms leave downwards, well clear of the chord */
        var southWest = _pointAt(-30f, -150f);
        var southEast = _pointAt(30f, -150f);

        _street(store, west, m, 1.0f);
        _street(store, m, east, 1.0f);
        _street(store, m, southWest, 1.0f);
        _street(store, m, southEast, 1.0f);

        /* the ring, for the reason given on _cross */
        _street(store, west, southWest, RingWeight);
        _street(store, east, southEast, RingWeight);

        var f = new Cross { Store = store, Middle = m };
        var report = _place(f);

        Assert.Equal(1, report.Considered);
        Assert.Equal(0, report.Placed);
        Assert.Equal(1, report.Refused[StructureRefusal.MissesWhatItCrosses]);
    }


    /**
     * A road that comes within the ramp's plan separation is what stops the structure,
     * and the control is the same road moved away.
     */
    [Theory]
    [InlineData(-160f, 0, 1)]
    [InlineData(-400f, 1, 0)]
    public void ARoadTooCloseToARampRefusesTheStructure(float atX, int placed, int refused)
    {
        var f = _cross();

        var a = _pointAt(atX, -60f);
        var b = _pointAt(atX, 60f);
        _street(f.Store, a, b, 0.5f);

        var report = _place(f);

        Assert.Equal(placed, report.Placed);
        report.Refused.TryGetValue(StructureRefusal.RampClearance, out int n);
        Assert.Equal(refused, n);
    }


    /**
     * ⚠️ The deck has to clear the road beneath it, and MinDeckClearance is derived
     * rather than chosen: MetaGen.ClusterNavigationHeight is the height the game's own
     * traffic flies at over the ground, so a deck that does not clear it is a wall.
     */
    [Fact]
    public void ADeckThatDoesNotClearTheRoadBeneathItIsRefused()
    {
        Assert.Equal(global::engine.world.MetaGen.ClusterNavigationHeight,
            StructurePlacer.MinDeckClearance);

        /*
         * The crossing and its own road stand 10 m above the feet, so the deck - which is
         * 8 m over the FEET - would pass 2 m below the road it flies over.
         */
        var high = _place(_cross(middleHeight: 10f));
        Assert.Equal(0, high.Placed);
        Assert.Equal(1, high.Refused[StructureRefusal.DeckClearance]);

        /*
         * Four metres lower and the deck clears it by more than traffic needs.
         */
        var ok = _place(_cross(middleHeight: 4f));
        Assert.Equal(1, ok.Placed);

        /*
         * One reading per road passing underneath, and on a straight corridor the deck
         * passes exactly through the crossing, so both of them read there.
         */
        Assert.Equal(2, ok.Clearances.Count);
        Assert.All(ok.Clearances, c => Assert.Equal(4f, c, 3));
    }


    /*
     * ============================================================ the deck bound =====
     */

    /**
     * ⚠️ THE BOUND: a deck is held to what the road it carries would be held to on the
     * ground, and never to more than its own ramps.
     *
     * No new number. MaxGradeFor is the one expression for "how steep may this be" and
     * this is it, capped - which binds below weight 0.69, where the interpolation for a
     * light street is above MaxRampGrade and a deck would otherwise be allowed to
     * out-climb the ramps leading to it.
     */
    [Theory]
    [InlineData(1.3f, 0.05f)]
    [InlineData(1.0f, 0.0745f)]
    [InlineData(0.5f, 0.1f)]
    [InlineData(0.2f, 0.1f)]
    public void ADeckIsHeldToItsOwnRoadsLimitCappedAtTheRampGrade(float weight, float expected)
    {
        var policy = new GradePolicy();
        var deck = new Stroke() { ClusterId = 0, Weight = weight, Kind = StrokeKind.Bridge };
        deck.A = _pointAt(0f, 0f, 1);
        deck.B = _pointAt(100f, 0f, 1);

        Assert.Equal(expected, policy.MaxDeckGradeFor(deck), 3);

        Assert.Equal(
            Single.Min(policy.MaxGradeFor(deck), policy.MaxRampGrade),
            policy.MaxDeckGradeFor(deck));
    }


    /**
     * ...and it is the corridor's own weight that decides, on one geometry: the same
     * crossing, the same terrain, refused as an arterial and built as an alley.
     *
     * The two feet are 16.8 m apart in height over a 240 m deck, which is 7 %: inside a
     * light street's bound of 10 % and outside a heavy one's of 5 %.
     */
    [Theory]
    [InlineData(1.3f, 0)]
    [InlineData(0.2f, 1)]
    public void TheSameCorridorIsRefusedAsAnArterialAndBuiltAsAnAlley(float weight, int placed)
    {
        var f = _cross(weight: weight);
        f.Ground[f.West.Id] = 0f;
        f.Ground[f.East.Id] = 16.8f;

        var report = _place(f);

        Assert.Equal(placed, report.Placed);

        if (0 == placed)
        {
            Assert.Equal(1, report.Refused[StructureRefusal.DeckGrade]);
        }
        else
        {
            Assert.Equal(0.07f, Single.Abs(report.DeckGrades.Single()), 3);
        }
    }


    /**
     * ⚠️ A grade here is the ROAD's grade, and the deck elevation in it hides on a deck.
     *
     * A ramp joins two decks, so dropping StreetPoint.LevelElevation understates its grade
     * by a whole deck height over its own length - which at MaxRampGrade is the entire
     * grade, 10 % reported as 0. A DECK has both ends on one level, so the term cancels
     * and every measurement of a deck agrees with or without it: the report's own deck
     * grades, the refusal, the end-to-end gate over seven cities. Dropping the term fails
     * NOTHING built out of decks, which is why this is driven on a ramp.
     */
    [Fact]
    public void AGradeMeasuredHereIsTheRoadsAndNotTheGroundsUnderIt()
    {
        var policy = new GradePolicy();

        var foot = _pointAt(0f, 0f);
        var deckEnd = _pointAt(
            OverpassBuilder.RampLengthFor(policy, 0, StrokeKind.Bridge), 0f, 1);

        var ramp = new Stroke()
        {
            ClusterId = 0, Weight = 1f, Kind = StrokeKind.Ramp, Level = 0, IsPrimary = false
        };
        ramp.A = foot;
        ramp.B = deckEnd;

        /*
         * Level ground at both ends: the ground says the ramp is flat, and the road climbs
         * a whole deck over exactly the length MaxRampGrade asks for.
         */
        var heights = new Dictionary<int, float> { [foot.Id] = 0f, [deckEnd.Id] = 0f };

        Assert.Equal(policy.MaxRampGrade, StructurePlacer.RoadGradeOf(ramp, heights), 5);
        Assert.NotEqual(0f, StructurePlacer.RoadGradeOf(ramp, heights));

        /*
         * ...and a deck, where the term cancels, reads the same either way - which is the
         * reason the assertion above is on a ramp.
         */
        var deckA = _pointAt(0f, 0f, 1);
        var deckB = _pointAt(100f, 0f, 1);
        var deck = new Stroke()
        {
            ClusterId = 0, Weight = 1f, Kind = StrokeKind.Bridge, Level = 1, IsPrimary = false
        };
        deck.A = deckA;
        deck.B = deckB;

        var deckHeights = new Dictionary<int, float> { [deckA.Id] = 0f, [deckB.Id] = 4f };

        Assert.Equal(0.04f, StructurePlacer.RoadGradeOf(deck, deckHeights), 5);
    }


    /**
     * ⚠️ The ramp half of the grade check is a BACKSTOP with no reachable path through
     * OverpassBuilder, and it is driven directly rather than pretended to be covered.
     *
     * A chain whose first member does not change level is one StructureProfile refuses to
     * design, which leaves its far end at whatever the terrain gave it - so the ramp
     * comes out at a grade nobody chose. A containment test could not tell that from a
     * design (§7p): the assertion is on the refusal that names it.
     */
    [Fact]
    public void ARampThatWasNeverDesignedIsRefusedOnItsMeasuredGrade()
    {
        var policy = new GradePolicy();

        var foot = _pointAt(0f, 0f);
        var flat = _pointAt(80f, 0f);
        var deckEnd = _pointAt(320f, 0f, 1);
        var far = _pointAt(400f, 0f);

        Stroke member(StreetPoint a, StreetPoint b, StrokeKind kind, sbyte level)
        {
            var s = new Stroke()
            {
                ClusterId = 0, Weight = 1f, Kind = kind, Level = level, IsPrimary = false
            };
            s.A = a;
            s.B = b;
            return s;
        }

        /*
         * A "ramp" both of whose ends are on the ground: malformed, and the only shape
         * that tells a designed profile from an assumed one.
         */
        var chain = new List<Stroke>
        {
            member(foot, flat, StrokeKind.Ramp, 0),
            member(flat, deckEnd, StrokeKind.Bridge, 1),
            member(deckEnd, far, StrokeKind.Ramp, 0)
        };

        var heights = new Dictionary<int, float>
        {
            [foot.Id] = 0f, [flat.Id] = 40f, [far.Id] = 0f
        };

        StructureProfile.Design(chain, heights, policy);

        Assert.Equal(StructureRefusal.RampGrade,
            StructurePlacer.JudgeGradesOf(chain, heights, policy));

        /*
         * The control: the same chain with the flat "ramp" put at the height its grade
         * permits passes the ramp check and is judged on its deck instead.
         */
        heights[flat.Id] = 4f;
        Assert.NotEqual(StructureRefusal.RampGrade,
            StructurePlacer.JudgeGradesOf(chain, heights, policy));
    }


    /**
     * Two crossings that would share a foot: the first is built and the second refused,
     * so no stroke is claimed twice and no junction ends up carrying two structures.
     */
    [Fact]
    public void TwoCorridorsSharingAFootDoNotBothGetOne()
    {
        var store = new StrokeStore(ClusterSize);

        var west = _pointAt(-200f, 0f);
        var m1 = _pointAt(0f, 0f);
        var shared = _pointAt(200f, 0f);
        var m2 = _pointAt(400f, 0f);
        var east = _pointAt(600f, 0f);

        var north1 = _pointAt(0f, 200f);
        var north2 = _pointAt(400f, 200f);
        var south1 = _pointAt(0f, -200f);
        var south2 = _pointAt(400f, -200f);

        _street(store, west, m1, 1.0f);
        _street(store, m1, shared, 1.0f);
        _street(store, shared, m2, 1.0f);
        _street(store, m2, east, 1.0f);

        _street(store, m1, north1, 1.0f);
        _street(store, m1, south1, 1.0f);
        _street(store, m2, north2, 1.0f);
        _street(store, m2, south2, 1.0f);

        /*
         * The two cross roads joined to each other, so that lifting the first crossing
         * leaves it reachable. `shared` keeps two arms and is therefore not a crossing of
         * its own.
         */
        _street(store, north1, north2, RingWeight);
        _street(store, south1, south2, RingWeight);

        var report = StructurePlacer.Place(
            store, 0, new GradePolicy(), _ => 0f, 19.7f, 10f, 0f);

        Assert.Equal(2, report.Considered);
        Assert.Equal(1, report.Placed);
        Assert.Equal(1, report.Refused[StructureRefusal.OverlapsAStructure]);
    }


    /**
     * A corridor bent enough that the chord between its two feet is shorter than the two
     * ramps standing in it. Reachable only with the deck's overhang switched off, which
     * is why it is a fixture: with the derived overhang, an arm long enough to hold a
     * ramp already makes a chord long enough to hold two.
     */
    [Fact]
    public void ACorridorWithNoRoomForTwoRampsIsNotAStructure()
    {
        var store = new StrokeStore(ClusterSize);

        var m = _pointAt(0f, 0f);
        var west = _pointAt(-81f, 0f);
        var east = _pointAt(72.9f, 35.3f);          /* 81 m out at a dot of -0.90 */

        _street(store, west, m, 1.0f);
        _street(store, m, east, 1.0f);
        _street(store, m, _pointAt(0f, -150f), 1.0f);
        _street(store, m, _pointAt(30f, -150f), 1.0f);

        var report = StructurePlacer.Place(
            store, 0, new GradePolicy(), _ => 0f, rampClearance: 0f, minSpanLength: 1f,
            maxSpanLength: 0f);

        Assert.Equal(1, report.Considered);
        Assert.Equal(1, report.Refused[StructureRefusal.NotAStructure]);
    }


    /*
     * ============================================================ IsPrimary ==========
     */

    /**
     * ⚠️ OverpassBuilder used to write IsPrimary = true onto all three members.
     *
     * It is an orientation bit that SuccessorEmitter flips per branch (§0.4), not a rank,
     * so a structure asserting it says something about the road it replaces that the road
     * did not say. Carried now - and asserted both ways round, because a hard-coded FALSE
     * would pass a test that only ever built from a secondary road.
     */
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStructureCarriesTheOrientationBitOfTheRoadItReplaces(bool isPrimary)
    {
        var chain = new OverpassBuilder(0).Build(
            _pointAt(0f, 0f), _pointAt(400f, 0f), StrokeKind.Bridge,
            rampLength: 80f, weight: 1f, isPrimary: isPrimary);

        Assert.All(chain, s => Assert.Equal(isPrimary, s.IsPrimary));
    }


    /**
     * ...and the placer takes it off the corridor rather than deciding.
     */
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThePlacerTakesTheOrientationBitFromTheCorridor(bool isPrimary)
    {
        var f = _cross();
        foreach (var s in f.Store.GetStrokes())
        {
            s.IsPrimary = isPrimary;
        }

        Assert.Equal(1, _place(f).Placed);

        Assert.All(
            f.Store.GetStrokes().Where(s => StrokeKinds.IsStructure(s.Kind)),
            s => Assert.Equal(isPrimary, s.IsPrimary));
    }


    /**
     * A deck kind that is not a span is refused rather than built as two ramps to
     * nowhere.
     */
    [Fact]
    public void OnlyABridgeOrATunnelCanBeTheMiddleOfAStructure()
    {
        foreach (var kind in new[]
                 { StrokeKind.Street, StrokeKind.ConnectorBridge, StrokeKind.Ramp })
        {
            Assert.Null(new OverpassBuilder(0).Build(
                _pointAt(0f, 0f), _pointAt(400f, 0f), kind, 80f, 1f, true));
        }

        Assert.NotNull(new OverpassBuilder(0).Build(
            _pointAt(0f, 0f), _pointAt(400f, 0f), StrokeKind.Bridge, 80f, 1f, true));
        Assert.NotNull(new OverpassBuilder(0).Build(
            _pointAt(0f, 0f), _pointAt(400f, 0f), StrokeKind.Tunnel, 80f, 1f, true));
    }


    /**
     * ⚠️ A ConnectorBridge is an ordinary ground road and one to three exist in every
     * shipped city (§0.7), but the placer does not rebuild one: it is the network's
     * repair rather than a street the ruleset laid, and it is not run through the
     * constraint pipeline at all (§7.8).
     */
    [Fact]
    public void AConnectorBridgeIsNotACorridorToLift()
    {
        var store = new StrokeStore(ClusterSize);

        var m = _pointAt(0f, 0f);
        var west = _pointAt(-200f, 0f);
        var east = _pointAt(200f, 0f);

        _street(store, west, m, 1.0f, StrokeKind.ConnectorBridge);
        _street(store, m, east, 1.0f);

        /*
         * Deliberately NOT opposed to each other: two arms that were would be a
         * straight-through pair in their own right, and the fixture would then be
         * measuring them rather than the connector.
         */
        _street(store, m, _pointAt(0f, 150f), 1.0f);
        _street(store, m, _pointAt(140f, -110f), 1.0f);

        var report = StructurePlacer.Place(
            store, 0, new GradePolicy(), _ => 0f, 19.7f, 10f, 0f);

        Assert.Equal(0, report.Considered);
        Assert.Equal(0, report.Placed);
    }


    /**
     * ⚠️ A junction a chain invents gets an id this network has not issued.
     *
     * A StreetPoint carries a process-global provisional id until the store gives it the
     * network's own, and a network's own start at 1 - so a deck end and a real junction
     * can carry the same number, and every height the placer judges on is looked up by
     * that number. The failure is silent: one junction answers another's question.
     *
     * ⚠️ ASSERTED AS EQUALITY WITH THE STORE'S OWN NEXT IDS, and it has to be. "No
     * invented junction collides" is satisfied by luck whenever the global counter
     * happens to sit above the network's range - which in this test host, after a dozen
     * cities have been built, it always does. Deleting the reservation outright fails
     * NOTHING if the gate is the absence of a collision: §7p's "a containment test cannot
     * tell a guess from a refusal", one more time.
     */
    [Fact]
    public void AnInventedJunctionIsGivenTheIdTheStoreItselfWouldGiveIt()
    {
        var f = _cross();

        int next = StructurePlacer.NextFreeIdIn(f.Store);
        Assert.Equal(f.Store.GetStreetPoints().Max(sp => sp.Id) + 1, next);

        var chain = new OverpassBuilder(0).Build(
            f.West, f.East, StrokeKind.Bridge, rampLength: 80f, weight: 1f, isPrimary: false);

        int after = StructurePlacer.ReserveIdsIn(f.Store, chain, next);

        Assert.Equal(next, chain[1].A.Id);
        Assert.Equal(next + 1, chain[1].B.Id);
        Assert.Equal(next + 2, after);

        /*
         * ...and the feet, which the store already knows about, keep the identity they
         * have. Renumbering one of those would move a real junction's height entry.
         */
        Assert.True(f.West.InStore && f.East.InStore);
        Assert.Equal(chain[0].A, f.West);
        Assert.True(f.West.Id < next && f.East.Id < next);
    }


    /**
     * ...and end to end: nothing a placed structure invented shares an id with anything
     * else in the finished network.
     */
    [Fact]
    public void NoTwoJunctionsOfACityWithStructuresShareAnIdentity()
    {
        foreach (var row in Seeds)
        {
            var (store, _, _) = _onTerrain((string)row[0], (float)row[1]);

            Assert.Equal(
                store.GetStreetPoints().Count,
                store.GetStreetPoints().Select(sp => sp.Id).Distinct().Count());
        }
    }


    /*
     * ============================================================ the wiring =========
     */

    /**
     * ⚠️ What the game hands the placer is the UNRELAXED ground, and it has to be.
     *
     * RelaxedStreetHeight's first act is to ask its cluster for the stroke store, which is
     * the thing being generated. Its base is a plain terrain sample with no such
     * dependency - and it is also the right quantity, because the placer relaxes the
     * network itself, over the ground strokes the finished city will have.
     */
    [Fact]
    public void TheUnrelaxedSourceIsTheOneUnderneathAndNotASecondSampler()
    {
        var cluster = StreetHarness.MakeCluster("wiring", 500f);

        var terrain = new FlatStreetHeight(cluster);
        var relaxed = new RelaxedStreetHeight(cluster, terrain, new GradePolicy());

        /*
         * By identity, never by "it answers the same": a second TerrainStreetHeight would
         * answer the same and keep a second cache, and two caches at one junction is two
         * heights at one junction, which is what IStreetHeightSource exists to prevent.
         */
        Assert.Same(terrain, StreetHeightSources.UnrelaxedOf(relaxed));
        Assert.Same(terrain, StreetHeightSources.UnrelaxedOf(terrain));
    }


    /**
     * ...and the game's one wiring of it, which no test can drive: reaching
     * ClusterDesc._generateStrokes needs ClusterStorage in the container and the elevation
     * cache behind it.
     *
     * Matched as a WHOLE EXPRESSION rather than by identifier, because §7n's lesson is
     * that a call is not an assignment and §7m's is that a mention is not a call - a scan
     * for "UnrelaxedOf" is satisfied by this very comment.
     */
    [Fact]
    public void TheGeneratorIsGivenTheUnrelaxedGroundInExactlyOnePlace()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string text = System.IO.File.ReadAllText(
            System.IO.Path.Combine(root, "engine", "world", "ClusterDesc.cs"));

        string squashed = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

        Assert.Contains(
            "streetGenerator.GroundHeightOf = "
            + "streets.StreetHeightSources.UnrelaxedOf(StreetHeightSource).GroundHeightAt;",
            squashed);
    }


    /**
     * ⚠️ Placement runs AFTER the connect pass, and only the source can say so.
     *
     * Both directions matter and neither is behaviourally observable on any pinned seed -
     * the placements come out identical either way, so the recorded flag-on fingerprint
     * says nothing about the order. What the order buys is this: ConnectComponentsPass
     * does NOT run the constraint pipeline (§7.8), so a ConnectorBridge laid after a
     * structure could cross a ramp with nothing checking, while a lift running after the
     * bridging cannot disconnect anything the bridging repaired.
     *
     * Labelled a scan, in the same shape and for the same reason as
     * HeavyFirstOrderingTests.TheConnectPassIsCalledExactlyOnceAndAfterTheDrain.
     */
    [Fact]
    public void ThePlacementPassIsCalledExactlyOnceAndAfterTheConnectPass()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(root, "engine", "streets", "Generator.cs"));

        Assert.Equal(1, source.Split("_place();").Length - 1);
        Assert.Equal(1, source.Split("private void _place()").Length - 1);

        int connect = source.IndexOf("_connectPass.Run();", StringComparison.Ordinal);
        int place = source.IndexOf("_place();", StringComparison.Ordinal);

        Assert.True(connect > 0 && place > connect,
            "the placement pass must be called after the connect pass, in the same method");

        Assert.DoesNotContain("return", source.Substring(connect, place - connect));
    }


    /**
     * A log target that keeps what was written to it.
     *
     * engine.Logger.SetLogTarget is a process global, so every assertion made on it here
     * looks for a fragment naming its own cluster or its own message; what other classes
     * log during the window is captured and ignored.
     */
    private sealed class LogCapture : global::engine.ILogTarget, IDisposable
    {
        private readonly List<string> _lines = new();
        private readonly object _lo = new();

        internal LogCapture() => global::engine.Logger.SetLogTarget(this);

        public void AddLogEntry(in global::engine.Logger.Level level, in string logEntry)
        {
            lock (_lo)
            {
                _lines.Add($"{level}|{logEntry}");
            }
        }

        internal bool Saw(string fragment)
        {
            lock (_lo)
            {
                return _lines.Any(l => l.Contains(fragment, StringComparison.Ordinal));
            }
        }

        public void Dispose() => global::engine.Logger.SetLogTarget(null);
    }
}
