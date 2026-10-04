using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * ⚠️ The relaxation ARRIVES, and says so when it does not.
 *
 * `GradeRelaxer` exhausted its whole 32 sweep budget on every generated city, and
 * `RelaxedStreetHeight` discarded the return value that said so - so since
 * joyce.DisableClusterFlattening became the default, every city in the game had been
 * built on an unconverged relaxation, in silence, for as long as the pass had existed.
 * Measured on the shipped terrain over the eight seeds StreetDeterminismTests pins, the
 * damage was not subtle: 8 to 743 strokes per city still over the grade their own weight
 * entitles them to, the worst at 31.2 % against a policy maximum of 14 %, and 7.88 m of
 * rise over the limit on one 1500 m city's stroke.
 *
 * Two things are gated here and they are not the same thing:
 *
 *   - the pass reaches a network in which every stroke is inside its own limit, within
 *     the budget, on real cities - with a positive control, because "it converged" is
 *     free on a network that needed no work at all;
 *   - and if it ever does not, that is a Warning naming the city rather than nothing.
 *
 * The flat city is here too, asserted as exact equality over whole generated cities
 * rather than argued from "a flat network has no over-limit stroke".
 */
public class GradeConvergenceTests
{
    private const float ClusterSize = 2000f;


    private static Dictionary<int, float> _terrainUnder(
        global::engine.world.ClusterDesc cd, StrokeStore store)
    {
        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = ShippedTerrain.HeightAt(
                cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y);
        }
        return heights;
    }


    /**
     * How far a stroke's rise exceeds what its own weight permits, in metres. Zero or
     * less is buildable.
     */
    private static float _overLimit(Stroke s, Dictionary<int, float> h, GradePolicy policy)
        => Single.Abs(h[s.B.Id] - h[s.A.Id]) - policy.MaxGradeFor(s) * s.Length;


    private static (int Count, float Worst, float WorstGrade) _violations(
        StrokeStore store, Dictionary<int, float> h, GradePolicy policy)
    {
        int n = 0;
        float worst = 0f, worstGrade = 0f;

        foreach (var s in store.GetStrokes())
        {
            if (s.Length < 0.001f) continue;

            float over = _overLimit(s, h, policy);
            if (over <= policy.ConvergenceEpsilon) continue;

            ++n;
            if (over > worst) worst = over;

            float grade = Single.Abs(h[s.B.Id] - h[s.A.Id]) / s.Length;
            if (grade > worstGrade) worstGrade = grade;
        }

        return (n, worst, worstGrade);
    }


    /*
     * ================================================ it arrives =====================
     */

    /**
     * ⚠️ THE GATE: a real city on the shipped terrain comes out buildable, inside the
     * budget.
     *
     * Every stroke within its own limit and a centimetre, and the sweep count strictly
     * below MaxSweeps - strictly, because reaching the cap is exactly the state this
     * whole round exists to remove and a test that tolerated it would tolerate the
     * defect.
     *
     * The positive control is the first assertion, not an afterthought: relaxing a
     * network that was already buildable converges in one sweep and satisfies everything
     * below vacuously. These cities start with tens to hundreds of unbuildable strokes,
     * so the pass is doing work and the work is what is being checked.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void AShippedCitySettlesInsideItsBudgetAndComesOutBuildable(string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);
        if (0 == store.GetStreetPoints().Count) return;

        var policy = new GradePolicy();
        var heights = _terrainUnder(cd, store);

        var before = _violations(store, heights, policy);
        Assert.True(before.Count >= 8,
            $"{idString}@{size}: the raw terrain gave only {before.Count} unbuildable "
            + "strokes, so this city cannot tell a relaxation from a no-op");

        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.True(sweeps < policy.MaxSweeps,
            $"{idString}@{size}: used all {sweeps} sweeps without settling");

        var after = _violations(store, heights, policy);
        Assert.True(0 == after.Count,
            $"{idString}@{size}: {after.Count} of {store.GetStrokes().Count} strokes are "
            + $"still over their limit after {sweeps} sweeps, the worst by "
            + $"{after.Worst:F2} m of rise at {after.WorstGrade:P1}");
    }


    /**
     * ...and the sweeps it takes, recorded, because a ruleset change that quietly
     * doubles them should be visible before it doubles again and hits the cap.
     *
     * The largest city the game builds is the largest number here by a factor of seven,
     * and it is under a third of the budget.
     */
    [Theory]
    [InlineData("seed000", 500f, 11)]
    [InlineData("seed011", 500f, 14)]
    [InlineData("Yelukhdidru", 400f, 28)]
    [InlineData("Yelukhdidru", 800f, 18)]
    [InlineData("seed000", 1500f, 57)]
    [InlineData("seed017", 2400f, 51)]
    [InlineData("Yelukhdidru", 3000f, 80)]
    public void TheSweepsAShippedCityNeedsAreRecorded(string idString, float size, int expected)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);

        var heights = _terrainUnder(cd, store);
        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.Equal(expected, sweeps);
    }


    /**
     * A settled sweep changes NOTHING, which is a stronger statement than "the
     * corrections have got small" and is the one two consecutive calls need.
     *
     * GradeRelaxer.Relax is two calls on a city with a structure in it - the anchor pass
     * and the sweep around the boundary - and if the settled anchor pass still applied
     * its last millimetres the second call would apply some more. It did, before this
     * was a property: adding a structure moved junctions on the far side of the city by
     * up to 4.3 mm, for no reason connected with the structure.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void RelaxingASettledCityAgainMovesNothing(string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);
        if (0 == store.GetStreetPoints().Count) return;

        var policy = new GradePolicy();
        var heights = _terrainUnder(cd, store);
        GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        var settled = new Dictionary<int, float>(heights);
        int again = GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.Equal(1, again);
        foreach (var kv in settled)
        {
            Assert.Equal(kv.Value, heights[kv.Key]);
        }
    }


    /**
     * The flat city does not move, asserted as equality over whole generated cities.
     *
     * Not argued from "a flat network has no over-limit stroke", which is true and is an
     * argument about the code rather than about the output. joyce.DisableClusterFlattening
     * defaults to true so this is no longer the shipped city, but the flat path is still
     * a world the setting describes and this round had no business touching it.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void AFlatCityIsUnchangedFloatForFloat(string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);
        var flat = new FlatStreetHeight(cd);

        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = flat.GroundHeightAt(sp);
        }

        var before = new Dictionary<int, float>(heights);
        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.Equal(before.Count > 0 ? 1 : 0, sweeps);
        foreach (var kv in before)
        {
            Assert.Equal(kv.Value, heights[kv.Key]);
        }
    }


    /*
     * ================================================ it says so =====================
     */

    /**
     * A log target that keeps what was written to it.
     *
     * engine.Logger.SetLogTarget is a process global; both tests that install one here
     * are in this class, so xUnit serialises them against each other.
     */
    private sealed class LogCapture : global::engine.ILogTarget, IDisposable
    {
        private readonly List<string> _lines = new();
        private readonly object _lo = new();

        internal LogCapture() => global::engine.Logger.SetLogTarget(this);

        public void AddLogEntry(in global::engine.Logger.Level level, in string logEntry)
        {
            lock (_lo) { _lines.Add($"{level}|{logEntry}"); }
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


    /**
     * ⚠️ Running out of sweeps is REPORTED, and the report names the city.
     *
     * The half of this round that is not arithmetic. GradeRelaxer.Relax has always
     * returned its sweep count "useful to a caller that wants to complain about it" and
     * there was no such caller, so a whole game's worth of unconverged cities went by
     * without a line anywhere.
     *
     * Driven through RelaxedStreetHeight.TableFor, which is the production expression -
     * the two lines left outside it are the store lookup and the memoisation, and a scan
     * below holds those. Asserted on the log because that is where the property lives; a
     * source scan for the identifier would be satisfied by a comment.
     */
    [Fact]
    public void RunningOutOfSweepsIsReportedAndNamesTheCity()
    {
        var cd = StreetHarness.MakeCluster("Yelukhdidru", 3000f);
        var store = StreetHarness.Generate("Yelukhdidru", 3000f);
        var terrain = ShippedTerrain.SourceOf(_terrainUnder(cd, store));

        using var log = new LogCapture();

        RelaxedStreetHeight.TableFor(
            cd, store.GetStrokes(), store.GetStreetPoints(), terrain,
            new GradePolicy { MaxSweeps = 4 });

        Assert.True(log.Saw("Yelukhdidru"),
            "the budget ran out and nothing in the log named the city it ran out on");
        Assert.True(log.Saw("without settling"));
    }


    /**
     * ...and the control, which is the half that makes the test above mean something: a
     * city that DOES settle says nothing at all.
     *
     * Without this, warning unconditionally passes.
     */
    [Fact]
    public void ACitySettlingWithinItsBudgetSaysNothing()
    {
        var cd = StreetHarness.MakeCluster("Yelukhdidru", 3000f);
        var store = StreetHarness.Generate("Yelukhdidru", 3000f);
        var terrain = ShippedTerrain.SourceOf(_terrainUnder(cd, store));

        using var log = new LogCapture();

        RelaxedStreetHeight.TableFor(
            cd, store.GetStrokes(), store.GetStreetPoints(), terrain, new GradePolicy());

        Assert.False(log.Saw("without settling"),
            "a city that settled inside its budget reported that it had not");
    }


    /**
     * The two lines TableFor was extracted out of still call it.
     *
     * RelaxedStreetHeight._ensureRelaxed needs ClusterStorage, MetaGen and the event
     * queue in the container to reach ClusterDesc.StrokeStore(), so it is not driven
     * here. Matched as a whole call expression rather than as a bare identifier: the
     * name alone survives in this file's own comments.
     */
    [Fact]
    public void TheHeightSourceStillGoesThroughTheReportingPath()
    {
        string source = File.ReadAllText(
            Path.Combine(_joyceRoot(), "engine", "streets", "RelaxedStreetHeight.cs"));

        Assert.Contains("var heights = TableFor(", source);
        Assert.Contains("store.GetStrokes(), store.GetStreetPoints(), _base, _policy)", source);
    }


    private static string _joyceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "JoyceCode.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.True(null != dir, "could not locate the test project directory");
        return Path.Combine(dir!.Parent!.Parent!.FullName, "JoyceCode");
    }


    /*
     * ================================================ the rule itself ================
     */

    /**
     * A correction puts the stroke it corrects exactly on its limit, in one sweep.
     *
     * The difference between a projection and a damped sweep, on the smallest fixture
     * that can show it. The old rule divided every correction by the busiest junction's
     * degree, so one sweep took a fraction of the excess off and the stroke crept to its
     * limit over a dozen more; there is no divisor now, because applying one stroke at a
     * time cannot overshoot.
     */
    [Fact]
    public void OneSweepPutsAnOverSteepStrokeExactlyOnItsLimit()
    {
        var cd = StreetHarness.MakeCluster("projection", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = new StreetPoint { ClusterId = 0 };
        a.SetPos(0f, 0f);
        var b = new StreetPoint { ClusterId = 0 };
        b.SetPos(0f, 0f);

        var stroke = Stroke.CreateByAngleFrom(cd, a, b, 0f, 100f, true, 0.5f);
        store.AddStroke(stroke);

        var policy = new GradePolicy { MaxSweeps = 1 };
        var heights = new Dictionary<int, float> { [a.Id] = 0f, [b.Id] = 60f };

        GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.Equal(
            policy.MaxGradeFor(stroke) * stroke.Length,
            heights[b.Id] - heights[a.Id], 3);
    }


    /**
     * ⚠️ A stroke too short to have a grade is left alone, and it had no test at all.
     *
     * Pre-existing, and a mutation found it: widening the guard so such a stroke IS
     * corrected passed the entire suite, because no generated city and no fixture anywhere
     * contains one. It is not harmless if it ever happens - the limit is grade times
     * length, so a stroke of nearly no length has a limit of nearly zero, is over it by
     * whatever its two ends differ by, and drags both of them together on every sweep until
     * the budget runs out.
     *
     * ⚠️ THREE things found while writing it, and together they say exactly how narrow the
     * guard's domain is - it is one micrometre to one millimetre, and nothing in the tree
     * can produce a stroke inside it:
     *
     *   - an EXACTLY zero length stroke never reaches the guard at all. `Stroke.Length`
     *     itself throws below 1e-6 m, and the relaxer reads `s.Length` on the line above
     *     the test;
     *   - `StreetPoint.SetPos` quantises to a **0.1 m** grid, so two junctions placed
     *     through it are either identical or 0.1 m apart - a hundred times the guard;
     *   - `StrokeStore.AddPoint` refuses a point "considerably close" to one it already
     *     has, so such a stroke cannot join a store either.
     *
     * So this writes `Pos` directly, which is public, and hands the strokes to `Relax` as a
     * plain list, which it accepts. That is the guard's whole reachable surface.
     */
    [Fact]
    public void AStrokeTooShortToHaveAGradeIsLeftAlone()
    {
        var cd = StreetHarness.MakeCluster("degenerate", ClusterSize);

        var a = new StreetPoint { ClusterId = 0 };
        a.SetPos(0f, 0f);
        var b = new StreetPoint { ClusterId = 0 };
        b.SetPos(0f, 0f);
        b.Pos = new System.Numerics.Vector2(0.0005f, 0f);

        /*
         * ...and an ordinary steep stroke beside it, so that "nothing moved" cannot be
         * satisfied by a relaxation that did nothing at all.
         */
        var c = new StreetPoint { ClusterId = 0 };
        c.SetPos(100f, 0f);

        var degenerate = Stroke.CreateByAngleFrom(cd, a, b, 0f, 100f, true, 0.5f);
        degenerate.B.Pos = new System.Numerics.Vector2(0.0005f, 0f);
        var ordinary = Stroke.CreateByAngleFrom(cd, b, c, 0f, 100f, true, 0.5f);

        Assert.True(degenerate.Length > 0f && degenerate.Length < 0.001f,
            $"the fixture must produce a stroke inside the guard's band, and it is "
            + $"{degenerate.Length}");

        var policy = new GradePolicy();
        var heights = new Dictionary<int, float>
        {
            [a.Id] = 0f,
            [b.Id] = 0f,
            [c.Id] = 60f
        };

        GradeRelaxer.Relax(new List<Stroke> { degenerate, ordinary }, heights, policy);

        Assert.NotEqual(60f, heights[c.Id]);
        Assert.Equal(0f, heights[a.Id]);
    }


    /**
     * A stroke inside its limit by less than ConvergenceEpsilon is left alone.
     *
     * The tolerance is one number and it does both jobs - what "settled" means and what
     * is worth correcting - which is what makes "settled" and "this sweep changed
     * nothing" the same statement. Applying it to only the exit test is the version that
     * moved a city by 4.3 mm when a structure was added to it.
     */
    [Fact]
    public void AStrokeWithinTheToleranceOfItsLimitIsNotTouched()
    {
        var cd = StreetHarness.MakeCluster("tolerance", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = new StreetPoint { ClusterId = 0 };
        a.SetPos(0f, 0f);
        var b = new StreetPoint { ClusterId = 0 };
        b.SetPos(0f, 0f);

        var stroke = Stroke.CreateByAngleFrom(cd, a, b, 0f, 100f, true, 0.5f);
        store.AddStroke(stroke);

        var policy = new GradePolicy();
        float limit = policy.MaxGradeFor(stroke) * stroke.Length;

        /*
         * Half a tolerance over, which is a fifth of a centimetre on a 100 m street.
         */
        var heights = new Dictionary<int, float>
        {
            [a.Id] = 0f,
            [b.Id] = limit + 0.5f * policy.ConvergenceEpsilon
        };

        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.Equal(1, sweeps);
        Assert.Equal(0f, heights[a.Id]);
        Assert.Equal(limit + 0.5f * policy.ConvergenceEpsilon, heights[b.Id]);
    }
}
