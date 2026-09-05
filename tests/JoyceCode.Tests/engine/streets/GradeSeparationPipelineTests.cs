using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B1: the machinery that was built and never wired.
 *
 * ClearanceConstraint and SpanLengthConstraint existed, had unit tests, and were absent
 * from Generator._buildPipeline; SplitStrokeAt had no Kind guard and reached AddStroke
 * directly, bypassing NetworkBuilder._checkLevels; ConnectComponentsPass chose its
 * bridge partner by plan distance alone, which stacks over a deck junction, and added
 * its strokes to the store itself.
 *
 * The whole of that was invisible to every gate for months, for one reason worth
 * stating up front: NO GENERATED CITY CONTAINS A RAMP. Unlimited real data cannot see a
 * rule that governs a shape the data does not have, which is why everything below is
 * driven over a hand-built store that already holds an overpass - and why the two
 * constraint tests are driven through Generator.Generate() rather than by calling a
 * constraint, since calling the constraint is exactly what passed while it was out of
 * the pipeline.
 */
public class GradeSeparationPipelineTests
{
    private const float ClusterSize = 600f;


    private static StreetPoint _pointAt(float x, float y, sbyte level = 0)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _stroke(
        StreetPoint a, StreetPoint b, StrokeKind kind, sbyte level, float weight = 1.0f)
    {
        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = weight, Kind = kind, Level = level
        };
        s.A = a;
        s.B = b;
        return s;
    }


    /**
     * A generator over the given store, seeded and bounded exactly as the game does it,
     * but with the seed strokes supplied by the caller so that the geometry under test
     * is known rather than whatever a cluster seed happened to produce.
     */
    private static Generator _generatorOver(StrokeStore store, ClusterDesc cd, string seed)
    {
        var g = new Generator();
        g.SetAnnotation("gradesep");
        g.Reset(seed, store, cd);
        StreetSeeds.ApplyBounds(g, cd);
        return g;
    }


    /**
     * The structure every test here stands on: two ground ramps along y = 0 with a
     * bridge deck between them, one level up.
     *
     * halfSpan is a parameter because the two ACs need different ramp lengths. The
     * clearance tests want a structure a growing network runs into; the split tests
     * want a ramp whose MIDDLE is more than 30 m from either end, because
     * StrokeNearPointConstraint snaps a candidate's far end onto any junction within
     * that distance and the candidate would then never reach the intersection check at
     * all. At halfSpan 100 the ramps are 50 m long and their midpoints are 25 m from
     * both ends, which is exactly that trap.
     */
    private static List<Stroke> _commitOverpassInto(StrokeStore store, float halfSpan = 100f)
    {
        var chain = new OverpassBuilder(0).Build(
            _pointAt(-halfSpan, 0f), _pointAt(halfSpan, 0f),
            StrokeKind.Bridge, rampFraction: 0.25f, weight: 1.2f);

        new NetworkBuilder(store).CommitChain(chain);
        return chain;
    }


    private static IEnumerable<Stroke> _ramps(StrokeStore store)
        => store.GetStrokes().Where(s => s.Kind == StrokeKind.Ramp);


    /**
     * True plan distance between two segments, measured independently of the store's
     * own query so that this cannot agree with a wrong implementation.
     *
     * The four endpoint-to-segment distances are the answer ONLY when the segments do
     * not cross; two segments crossing at their midpoints have all four endpoints far
     * away and a distance of zero. StrokeStore.GetRampsNear was written with just those
     * four terms and therefore reported nothing for a street laid straight through a
     * ramp - the one case the clearance rule exists for.
     */
    private static float _planDistance(Stroke s, Stroke t)
    {
        if (null != s.Intersects(t)) return 0f;

        return Single.Min(
            Single.Min(t.Distance(s.A.Pos), t.Distance(s.B.Pos)),
            Single.Min(s.Distance(t.A.Pos), s.Distance(t.B.Pos)));
    }


    /**
     * Plan distance from a stroke to the nearest ramp it does NOT share a junction
     * with - sharing a junction with a ramp is how you get onto it.
     */
    private static float _clearanceOf(Stroke s, StrokeStore store)
    {
        float worst = Single.MaxValue;

        foreach (var ramp in _ramps(store))
        {
            if (ramp.A == s.A || ramp.A == s.B || ramp.B == s.A || ramp.B == s.B) continue;

            float d = _planDistance(s, ramp);
            if (d < worst) worst = d;
        }

        return worst;
    }


    /**
     * AC B1.1 - ClearanceConstraint is in the pipeline.
     *
     * Driven through Generate() over a store that already holds an overpass. The seeds
     * are laid across the ramp corridor so that generation genuinely tries to build
     * there; with the constraint removed from Generator._buildPipeline this test fails
     * with strokes 0 m from a ramp.
     *
     * ConnectorBridge strokes are excluded from the assertion on purpose and NOT
     * because they are allowed to be near a ramp: ConnectComponentsPass does not run
     * the constraint pipeline at all, which is a real gap and is recorded as one.
     */
    [Fact]
    public void NoGeneratedStreetComesWithinTheRampClearance()
    {
        var cd = StreetHarness.MakeCluster("gradesep-clearance", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        _commitOverpassInto(store);

        var g = _generatorOver(store, cd, "streets-gradesep-clearance");
        g.EnableGradeSeparation = true;
        g.RampClearance = 20f;

        _seedAcrossTheCorridor(g, cd);
        g.Generate();

        var grown = store.GetStrokes().Where(s => s.Kind == StrokeKind.Street).ToList();

        Assert.True(grown.Count >= 10,
            $"the fixture grew only {grown.Count} streets, so it cannot show anything "
            + "about a rule that rejects some of them");

        var offenders = grown.Where(s => _clearanceOf(s, store) < 20f).ToList();

        Assert.True(0 == offenders.Count,
            $"{offenders.Count} of {grown.Count} generated streets are closer than 20 m "
            + $"to a ramp they do not join, the closest at "
            + $"{(offenders.Count > 0 ? offenders.Min(s => _clearanceOf(s, store)) : 0f):F2} m");
    }


    /**
     * The case the clearance rule exists for, and the one it used to miss: a street
     * laid straight THROUGH a ramp.
     *
     * It cannot be split - a structure is invisible to the intersection query - so if
     * clearance does not refuse it, the road simply passes through the ramp with
     * nothing anywhere recording that it did. Identical geometry to
     * ACandidateCrossingARampDoesNotSplitIt, which runs with clearance switched off and
     * lets exactly this happen.
     */
    [Fact]
    public void AStreetLaidStraightThroughARampIsRefused()
    {
        var cd = StreetHarness.MakeCluster("gradesep-through", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        var chain = _commitOverpassInto(store, halfSpan: 200f);
        Stroke ramp = chain[0];

        var g = _generatorOver(store, cd, "streets-gradesep-through");
        g.EnableGradeSeparation = true;
        g.RampClearance = 20f;

        var a = _pointAt(-150f, -60f);
        var b = new StreetPoint() { ClusterId = 0 };
        var candidate = Stroke.CreateByAngleFrom(cd, a, b, 0.5f * Single.Pi, 120f, true, 1.1f);

        /*
         * Every endpoint of both is 50 m or more from the other segment, so the four
         * endpoint-to-segment distances the store used to test alone all clear 20 m
         * comfortably. The segments still cross.
         */
        Assert.True(ramp.Distance(candidate.A.Pos) > 20f);
        Assert.True(ramp.Distance(candidate.B.Pos) > 20f);
        Assert.True(candidate.Distance(ramp.A.Pos) > 20f);
        Assert.True(candidate.Distance(ramp.B.Pos) > 20f);
        Assert.Equal(0f, _planDistance(candidate, ramp));

        g.AddStartingStroke(candidate);
        g.Generate();

        Assert.DoesNotContain(store.GetStrokes(),
            s => s.Kind == StrokeKind.Street && _clearanceOf(s, store) < 20f);
    }


    /**
     * The control for the test above: the identical run with clearance not supplied
     * DOES build inside the corridor. Without it the assertion could be satisfied by a
     * generator that never went near a ramp in the first place.
     */
    [Fact]
    public void WithoutClearanceTheSameRunBuildsRightBesideTheRamps()
    {
        var cd = StreetHarness.MakeCluster("gradesep-clearance", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        _commitOverpassInto(store);

        var g = _generatorOver(store, cd, "streets-gradesep-clearance");
        g.EnableGradeSeparation = false;

        _seedAcrossTheCorridor(g, cd);
        g.Generate();

        var grown = store.GetStrokes().Where(s => s.Kind == StrokeKind.Street).ToList();
        var offenders = grown.Where(s => _clearanceOf(s, store) < 20f).ToList();

        Assert.True(offenders.Count > 0,
            "with clearance off, this fixture is supposed to build inside the ramp "
            + "corridor - if it no longer does, the clearance test above proves nothing");
    }


    /**
     * Seeds laid deliberately across and along the ramp corridor.
     */
    private static void _seedAcrossTheCorridor(Generator g, ClusterDesc cd)
    {
        foreach (var (x, y, angle) in new[]
                 {
                     (-120f, -80f, 0.5f * Single.Pi),
                     (0f, -80f, 0.5f * Single.Pi),
                     (120f, -80f, 0.5f * Single.Pi),
                     (-160f, 40f, 0f),
                 })
        {
            var a = _pointAt(x, y);
            var b = new StreetPoint() { ClusterId = 0 };
            g.AddStartingStroke(
                Stroke.CreateByAngleFrom(cd, a, b, angle, 90f, true, 1.1f));
        }
    }


    /**
     * AC B1.2 - no ground stroke is ever split on a ramp.
     *
     * Clearance is deliberately switched OFF here (RampClearance = 0), so the only
     * thing standing between the candidate and a junction halfway up the ramp is that
     * StrokeStore.IntersectsMayTouchClosest does not report a structure. Restore the
     * report and this fails: the ramp becomes two strokes with a junction in the middle.
     */
    [Fact]
    public void ACandidateCrossingARampDoesNotSplitIt()
    {
        var cd = StreetHarness.MakeCluster("gradesep-nosplit", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        var chain = _commitOverpassInto(store, halfSpan: 200f);
        Stroke ramp = chain[0];

        StreetPoint rampA = ramp.A, rampB = ramp.B;

        var g = _generatorOver(store, cd, "streets-gradesep-nosplit");
        g.EnableGradeSeparation = true;
        g.RampClearance = 0f;

        /*
         * Straight across the middle of the first ramp, at right angles, and 50 m from
         * either of its ends.
         */
        var a = _pointAt(-150f, -60f);
        var b = new StreetPoint() { ClusterId = 0 };
        g.AddStartingStroke(Stroke.CreateByAngleFrom(cd, a, b, 0.5f * Single.Pi, 120f, true, 1.1f));

        g.Generate();

        Assert.Contains(ramp, store.GetStrokes());
        Assert.Same(rampA, ramp.A);
        Assert.Same(rampB, ramp.B);
        Assert.Single(store.GetStrokes().Where(s => s.Kind == StrokeKind.Ramp
                                                    && s.A.Pos == rampA.Pos));

        /*
         * And nothing was filed onto the ramp's interior either.
         */
        foreach (var sp in store.GetStreetPoints())
        {
            if (sp == rampA || sp == rampB) continue;

            Assert.True(ramp.Distance(sp.Pos) > 0.5f,
                $"junction {sp.Pos} sits on the ramp {rampA.Pos}..{rampB.Pos}");
        }
    }


    /**
     * The positive control B1.2 asks for: the same candidate crossing an ORDINARY
     * street, at the same place, does split it. Without this the test above passes for
     * a generator that simply never reached the crossing.
     */
    [Fact]
    public void TheSameCandidateCrossingAnOrdinaryStreetDoesSplitIt()
    {
        var cd = StreetHarness.MakeCluster("gradesep-nosplit", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        /*
         * Same plan geometry as the ramp in the test above, on the ground, and an
         * ordinary street.
         */
        var street = _stroke(_pointAt(-200f, 0f), _pointAt(-100f, 0f), StrokeKind.Street, 0, 1.2f);
        new NetworkBuilder(store).Commit(street);

        var g = _generatorOver(store, cd, "streets-gradesep-nosplit");
        g.EnableGradeSeparation = true;
        g.RampClearance = 0f;

        var a = _pointAt(-150f, -60f);
        var b = new StreetPoint() { ClusterId = 0 };
        g.AddStartingStroke(Stroke.CreateByAngleFrom(cd, a, b, 0.5f * Single.Pi, 120f, true, 1.1f));

        g.Generate();

        var halves = store.GetStrokes()
            .Where(s => Single.Abs(s.A.Pos.Y) < 0.01f && Single.Abs(s.B.Pos.Y) < 0.01f)
            .ToList();

        Assert.True(halves.Count >= 2,
            $"the crossing candidate should have split the street in two, found "
            + $"{halves.Count} strokes along y=0");

        Assert.Contains(store.GetStreetPoints(),
            sp => Single.Abs(sp.Pos.Y) < 0.01f
                  && sp.Pos.X > -199.9f && sp.Pos.X < -100.1f);
    }


    /**
     * AC B1.1 for the second constraint. A Bridge candidate shorter than MinSpanLength
     * has to be refused by the pipeline; drop SpanLengthConstraint from
     * Generator._buildPipeline and it is added to the store.
     *
     * 35 m is chosen so that MinLengthConstraint (30 m) has nothing to say about it -
     * otherwise this would pass with SpanLengthConstraint gone.
     */
    [Fact]
    public void ATooShortDeckIsRefusedByThePipeline()
    {
        var cd = StreetHarness.MakeCluster("gradesep-span", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var g = _generatorOver(store, cd, "streets-gradesep-span");
        g.EnableGradeSeparation = true;
        g.MinSpanLength = 40f;
        g.MaxSpanLength = 200f;

        g.AddStartingStroke(
            _stroke(_pointAt(-17.5f, 200f, 1), _pointAt(17.5f, 200f, 1), StrokeKind.Bridge, 1));

        g.Generate();

        Assert.DoesNotContain(store.GetStrokes(), s => s.Kind == StrokeKind.Bridge);
    }


    [Fact]
    public void ATooLongDeckIsRefusedByThePipeline()
    {
        var cd = StreetHarness.MakeCluster("gradesep-span", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var g = _generatorOver(store, cd, "streets-gradesep-span");
        g.EnableGradeSeparation = true;
        g.MinSpanLength = 40f;
        g.MaxSpanLength = 200f;

        g.AddStartingStroke(
            _stroke(_pointAt(-125f, 200f, 1), _pointAt(125f, 200f, 1), StrokeKind.Bridge, 1));

        g.Generate();

        Assert.DoesNotContain(store.GetStrokes(), s => s.Kind == StrokeKind.Bridge);
    }


    /**
     * The control: a deck of admissible length goes in. Without it both tests above
     * pass for a pipeline that refuses every Bridge for some unrelated reason.
     */
    [Fact]
    public void ADeckOfAdmissibleLengthIsAccepted()
    {
        var cd = StreetHarness.MakeCluster("gradesep-span", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var g = _generatorOver(store, cd, "streets-gradesep-span");
        g.EnableGradeSeparation = true;
        g.MinSpanLength = 40f;
        g.MaxSpanLength = 200f;

        g.AddStartingStroke(
            _stroke(_pointAt(-50f, 200f, 1), _pointAt(50f, 200f, 1), StrokeKind.Bridge, 1));

        g.Generate();

        Assert.Contains(store.GetStrokes(), s => s.Kind == StrokeKind.Bridge);
    }


    /**
     * AC B1.5, in code rather than by measurement: with the flag off the context the
     * pipeline runs on carries no clearance at all, so ClearanceConstraint returns
     * before it can reach StrokeStore.GetRampsNear - which allocates two lists on every
     * call and is what trips the allocation gate when it is supplied unconditionally.
     */
    [Fact]
    public void TheFlagOffPipelineIsHandedNoStructureTunablesAtAll()
    {
        var cd = StreetHarness.MakeCluster("gradesep-off", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        _commitOverpassInto(store, halfSpan: 200f);

        var g = _generatorOver(store, cd, "streets-gradesep-off");
        g.EnableGradeSeparation = false;
        g.RampClearance = 500f;
        g.MinSpanLength = 500f;

        /*
         * A stroke straight across the first ramp, and a 35 m deck. With the tunables
         * suppressed both are judged as if no structure existed, so both go in - even
         * though the properties named on the generator would refuse both.
         */
        var a = _pointAt(-150f, -60f);
        var b = new StreetPoint() { ClusterId = 0 };
        g.AddStartingStroke(Stroke.CreateByAngleFrom(cd, a, b, 0.5f * Single.Pi, 120f, true, 1.1f));
        g.AddStartingStroke(
            _stroke(_pointAt(-17.5f, 250f, 1), _pointAt(17.5f, 250f, 1), StrokeKind.Bridge, 1));

        g.Generate();

        Assert.Contains(store.GetStrokes(), s => s.Kind == StrokeKind.Bridge);
        Assert.Contains(store.GetStrokes(),
            s => s.Kind == StrokeKind.Street && _clearanceOf(s, store) < 20f);
    }


    /**
     * WHERE THE TWO CONSTRAINTS SIT, asserted rather than described.
     *
     * ICandidateConstraint's own warning says the order is part of the generated
     * output, and until now it was pinned only by the eight recorded fingerprints -
     * which say nothing at all about two constraints that are no-ops in a flag-off
     * city. So the order is written out here.
     *
     * The two new entries are after StrokeNearPointConstraint, the last constraint that
     * can return Restart: everything above it may still pull the candidate's far end
     * onto an existing junction, and a Reject placed before those would throw away a
     * candidate that was about to snap clear of the ramp it is being rejected for. And
     * before IntersectionConstraint, much the most expensive check here, so a candidate
     * that is going to be refused does not pay for it; span length first of the two
     * because it is pure arithmetic while clearance queries the octree.
     */
    [Fact]
    public void TheConstraintPipelineRunsInThisOrder()
    {
        var cd = StreetHarness.MakeCluster("gradesep-order", ClusterSize);
        var g = _generatorOver(new StrokeStore(ClusterSize), cd, "streets-gradesep-order");

        var build = typeof(Generator).GetMethod("_buildPipeline",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(build);
        build.Invoke(g, null);

        var field = typeof(Generator).GetField("_pipeline",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        var names = ((ICandidateConstraint[])field.GetValue(g)).Select(c => c.Name).ToArray();

        Assert.Equal(new[]
        {
            "min-length",
            "snap-to-nearby-point",
            "already-connected",
            "angle-separation-a",
            "angle-separation-b",
            "stroke-near-point",
            "point-near-stroke",
            "span-length",
            "clearance",
            "intersection",
        }, names);
    }


    /**
     * The derived defaults, pinned on the expression rather than the number: the widest
     * carriageway the ruleset can build. A literal here would silently stop matching
     * the day StreetWidth or weightMax changed.
     */
    [Fact]
    public void TheDefaultClearanceIsTheWidestCarriagewayTheRulesetCanBuild()
    {
        var g = new Generator();

        Assert.Equal(Stroke.WidthForWeight(g.weightMax), g.RampClearance);
        Assert.Equal(Stroke.WidthForWeight(g.weightMax), g.MinSpanLength);
        Assert.Equal(0f, g.MaxSpanLength);

        g.RampClearance = 33f;
        Assert.Equal(33f, g.RampClearance);
    }


    /**
     * The hoist that derivation depends on: a stroke's width and the width answered for
     * a weight are the same expression, not two copies of it.
     *
     * The equality below cannot say that on its own - inlining the arithmetic back into
     * StreetWidth() satisfies it perfectly, which is what a mutation showed - so the
     * count is asserted too. One expression is the property; two that happen to agree
     * is the defect this codebase keeps finding.
     */
    [Fact]
    public void AStrokesWidthIsTheWidthAnsweredForItsWeight()
    {
        foreach (float w in new[] { -1f, 0f, 0.2f, 0.7f, 1.0f, 1.3f })
        {
            var s = new Stroke() { ClusterId = 0, Weight = w };
            Assert.Equal(Stroke.WidthForWeight(w), s.StreetWidth());
        }

        string source = File.ReadAllText(Path.Combine(
            _repoRoot(), "JoyceCode", "engine", "streets", "Stroke.cs"));

        int copies = source.Split("8f + w * 9f").Length - 1;
        Assert.True(1 == copies,
            $"the carriageway width expression appears {copies} times in Stroke.cs; it "
            + "is WidthForWeight and StreetWidth() calls it");
    }


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    /**
     * The escape in ClearanceConstraint: a candidate that SHARES a junction with the
     * ramp is how you get onto the ramp, and it is necessarily within clearance of it.
     * Remove the escape and no road can ever reach a structure's foot - the structure
     * becomes unreachable, which is a worse failure than the one clearance prevents and
     * is completely silent.
     */
    [Fact]
    public void AStreetMayLeaveTheJunctionAtTheFootOfARamp()
    {
        var cd = StreetHarness.MakeCluster("gradesep-foot", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        var chain = _commitOverpassInto(store, halfSpan: 200f);
        StreetPoint foot = chain[0].A;

        var g = _generatorOver(store, cd, "streets-gradesep-foot");
        g.EnableGradeSeparation = true;
        g.RampClearance = 20f;

        /*
         * 60 degrees off the ramp - far enough apart that AngleSeparationConstraint has
         * nothing to say - and starting at the ramp's own foot, so its plan distance to
         * the ramp is zero.
         */
        var b = new StreetPoint() { ClusterId = 0 };
        var approach = Stroke.CreateByAngleFrom(
            cd, foot, b, Single.Pi / 3f, 100f, true, 1.1f);

        g.AddStartingStroke(approach);
        g.Generate();

        Assert.Contains(store.GetStrokes(),
            s => s.Kind == StrokeKind.Street && (s.A == foot || s.B == foot));
    }


    /**
     * A ramp only obstructs the decks it actually reaches. One that climbs from the
     * ground to level 1 is no more relevant to a street on level 2 than an unrelated
     * road is - and refusing there would make every deck above the first unbuildable
     * anywhere near a structure.
     */
    [Fact]
    public void ARampObstructsOnlyTheDecksItReaches()
    {
        foreach (sbyte level in new sbyte[] { 1, 2 })
        {
            var cd = StreetHarness.MakeCluster("gradesep-decks", ClusterSize);
            var store = new StrokeStore(ClusterSize);
            _commitOverpassInto(store, halfSpan: 200f);

            var g = _generatorOver(store, cd, "streets-gradesep-decks");
            g.EnableGradeSeparation = true;
            g.RampClearance = 20f;

            /*
             * Straight over the middle of the ramp that joins level 0 to level 1.
             */
            var a = _pointAt(-150f, -60f, level);
            var b = new StreetPoint() { ClusterId = 0, Level = level };
            var cand = Stroke.CreateByAngleFrom(cd, a, b, 0.5f * Single.Pi, 120f, true, 1.1f);
            cand.Level = level;

            g.AddStartingStroke(cand);
            g.Generate();

            bool built = store.GetStrokes().Any(s => s.Level == level && s.Kind == StrokeKind.Street);

            if (level == 1)
            {
                Assert.False(built,
                    "a street on level 1 crosses the ramp's upper end and must be refused");
            }
            else
            {
                Assert.True(built,
                    "a street on level 2 is two decks above the ramp and has nothing to "
                    + "do with it");
            }
        }
    }


    /**
     * AC B1.4's backstop. The mechanism is the verdict - a structure is not reported by
     * the intersection query at all - and this is what would have caught the original
     * defect, in which SplitStrokeAt called AddStroke directly and never reached
     * NetworkBuilder._checkLevels.
     */
    [Fact]
    public void SplittingAStructureIsRefusedOutright()
    {
        var store = new StrokeStore(ClusterSize);
        var chain = _commitOverpassInto(store);

        foreach (var member in chain)
        {
            var at = _pointAt(
                0.5f * (member.A.Pos.X + member.B.Pos.X),
                0.5f * (member.A.Pos.Y + member.B.Pos.Y),
                member.Level);

            Assert.Throws<InvalidOperationException>(
                () => new NetworkBuilder(store).SplitStrokeAt(member, at));
        }

        /*
         * And nothing was half done on the way: the refusal happens before the stroke
         * is taken out of the store.
         */
        Assert.Equal(3, store.GetStrokes().Count);
        Assert.Equal(4, store.GetStreetPoints().Count);
    }


    /**
     * A split point on another deck is refused too. Not reachable from the generator -
     * IntersectionConstraint gives the point the candidate's own level - but the pair
     * of halves it would produce each join two decks, which is the one thing the level
     * model forbids.
     */
    [Fact]
    public void SplittingAtAPointOnAnotherDeckIsRefused()
    {
        var store = new StrokeStore(ClusterSize);
        var street = _stroke(_pointAt(0f, 0f), _pointAt(100f, 0f), StrokeKind.Street, 0);
        new NetworkBuilder(store).Commit(street);

        Assert.Throws<InvalidOperationException>(
            () => new NetworkBuilder(store).SplitStrokeAt(street, _pointAt(50f, 0f, 1)));

        Assert.Single(store.GetStrokes());
    }


    /**
     * An ordinary street still splits, on both decks. The guard above must not have
     * been bought by refusing everything.
     */
    [Fact]
    public void AnOrdinaryStreetStillSplitsOnEitherDeck()
    {
        foreach (sbyte level in new sbyte[] { 0, 1 })
        {
            var store = new StrokeStore(ClusterSize);
            var street = _stroke(
                _pointAt(0f, 0f, level), _pointAt(100f, 0f, level), StrokeKind.Street, level);
            new NetworkBuilder(store).Commit(street);

            var tail = new NetworkBuilder(store).SplitStrokeAt(street, _pointAt(50f, 0f, level));

            Assert.Equal(2, store.GetStrokes().Count);
            Assert.Equal(level, tail.Level);
        }
    }


    /**
     * A ConnectorBridge is an ordinary ground road and must keep behaving like one.
     * Every shipped flat city contains one to three of them, so a rule phrased as
     * "anything that is not a Street" would move the default city.
     */
    [Fact]
    public void AConnectorBridgeIsNotAStructure()
    {
        Assert.False(StrokeKinds.IsStructure(StrokeKind.ConnectorBridge));
        Assert.False(StrokeKinds.IsStructure(StrokeKind.Street));
        Assert.True(StrokeKinds.IsStructure(StrokeKind.Ramp));
        Assert.True(StrokeKinds.IsStructure(StrokeKind.Bridge));
        Assert.True(StrokeKinds.IsStructure(StrokeKind.Tunnel));

        var store = new StrokeStore(ClusterSize);
        var connector = _stroke(
            _pointAt(0f, 0f), _pointAt(100f, 0f), StrokeKind.ConnectorBridge, 0);
        new NetworkBuilder(store).Commit(connector);

        /*
         * Visible to the intersection query, and splittable.
         */
        var cand = _stroke(_pointAt(50f, -50f), _pointAt(50f, 50f), StrokeKind.Street, 0);
        Assert.Equal(VerdictKind.Split,
            new IntersectionConstraint().Check(cand, store, ConstraintFixture.Context()).Kind);

        new NetworkBuilder(store).SplitStrokeAt(connector, _pointAt(50f, 0f));
        Assert.Equal(2, store.GetStrokes().Count);
    }


    /**
     * A crossing junction belongs to the deck the crossing happened on. Level 0 is the
     * default, so this is a no-op for every ground-only city - which is exactly why it
     * could have stayed wrong indefinitely.
     */
    [Fact]
    public void ACrossingJunctionIsOnTheDeckItWasFoundOn()
    {
        foreach (sbyte level in new sbyte[] { 0, 1, -1 })
        {
            var store = new StrokeStore(ClusterSize);
            new NetworkBuilder(store).Commit(_stroke(
                _pointAt(0f, 0f, level), _pointAt(100f, 0f, level), StrokeKind.Street, level));

            var cand = _stroke(
                _pointAt(50f, -50f, level), _pointAt(50f, 50f, level), StrokeKind.Street, level);

            var verdict = new IntersectionConstraint().Check(
                cand, store, ConstraintFixture.Context());

            Assert.Equal(VerdictKind.Split, verdict.Kind);
            Assert.Equal(level, verdict.SplitPoint.Level);
        }
    }
}
