using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B2: the queue drains heaviest first with the flag on, and is today's stack without it.
 *
 * Decision D2 removed the arterial stage, the re-seeding walk and the third rule table
 * from Phase B, leaving this as the whole mechanism by which a structure gets placed on a
 * heavy corridor before side streets attach to it. That makes the ordering the feature,
 * not an optimisation, and it has to be gated as one.
 *
 * ⚠️ WHY EVERY GATE HERE IS DRIVEN THROUGH Generate() OVER GENERATED CITIES. A test that
 * asked the queue how it compares two candidates passes perfectly with the queue unwired
 * from the generator - which is precisely what happened to ClearanceConstraint and
 * SpanLengthConstraint, both of which had passing unit tests for months while sitting
 * outside the pipeline (WP-B1 §7). So the ordering is observed where it actually happens:
 * in the order candidates leave the queue during a real run. CandidateQueue's own contract
 * is asserted too, at the bottom, but it is the weaker half and is labelled as such.
 */
public class HeavyFirstOrderingTests
{
    /**
     * The seeds worth draining twice. seed017@2400 and Yelukhdidru@3000 are the two
     * large ones; the small ones are here because a rule that only shows up at scale is
     * a rule nobody can debug.
     */
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000",     500f  },
        new object[] { "seed011",     500f  },
        new object[] { "seed000",     1500f },
        new object[] { "seed017",     2400f },
        new object[] { "Yelukhdidru", 800f  },
        new object[] { "Yelukhdidru", 3000f },
    };


    private static string _key(string idString, float size) => $"{idString}@{size:F0}";


    /**
     * One observation of the drain: the candidate that came out, and the heaviest one
     * that was left behind.
     */
    private readonly struct Pop
    {
        internal readonly Stroke Candidate;
        internal readonly float HeaviestLeft;

        internal Pop(Stroke candidate, IReadOnlyList<Stroke> pending)
        {
            Candidate = candidate;

            float heaviest = Single.NegativeInfinity;
            for (int i = 0; i < pending.Count; ++i)
            {
                if (pending[i].Weight > heaviest) heaviest = pending[i].Weight;
            }

            HeaviestLeft = heaviest;
        }
    }


    private static List<Pop> _drainOf(string idString, float size, bool heavyFirst)
    {
        var pops = new List<Pop>();
        StreetHarness.Generate(idString, size, null, heavyFirst,
            (candidate, pending) => pops.Add(new Pop(candidate, pending)));

        return pops;
    }


    /**
     * A candidate emitted by one of the two branch rules. The emitter tags a stroke with
     * the name of the rule that made it, so this is the rule's own identity rather than a
     * guess from geometry.
     */
    private static bool _isBranch(Stroke s)
        => s.Creator.Contains(":right") || s.Creator.Contains(":left");


    /**
     * B2.2, over whole generated cities: with the flag on, nothing heavier than the
     * candidate being judged is ever left waiting.
     *
     * That is the ordering stated as an invariant rather than as a comparison. It is what
     * makes "the heavy corridor is finished before its branches are popped" true, because
     * a branch is emitted from an already accepted stroke and drawn from a weight group
     * whose decrease probability is 190 of 256.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheHeaviestPendingCandidateIsAlwaysTheOnePopped(string idString, float size)
    {
        var pops = _drainOf(idString, size, heavyFirst: true);

        Assert.True(pops.Count > 20,
            $"{_key(idString, size)}: only {pops.Count} candidates were judged, which is "
            + "too few for this to say anything about the ordering");

        var violations = pops.Where(p => p.HeaviestLeft > p.Candidate.Weight).ToList();

        Assert.True(0 == violations.Count,
            $"{_key(idString, size)}: {violations.Count} of {pops.Count} candidates were "
            + $"popped while something heavier waited; the worst popped "
            + $"{violations.FirstOrDefault().Candidate?.Weight:F3} with "
            + $"{(violations.Count > 0 ? violations.Max(v => v.HeaviestLeft) : 0f):F3} "
            + "still pending");
    }


    /**
     * THE CONTROL, and the thing that gives the test above teeth: today's stack is
     * emphatically not heavy first.
     *
     * Without this, the invariant could be satisfied by a generator whose candidates
     * happen to arrive in descending weight anyway - and the whole ordering could be
     * unwired without a single test noticing.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TodaysStackIsNotHeavyFirst(string idString, float size)
    {
        var pops = _drainOf(idString, size, heavyFirst: false);

        var violations = pops.Where(p => p.HeaviestLeft > p.Candidate.Weight).ToList();

        Assert.True(violations.Count > pops.Count / 20,
            $"{_key(idString, size)}: the plain stack popped a lighter candidate ahead of "
            + $"a heavier one only {violations.Count} times in {pops.Count}; if the two "
            + "orderings barely differ on this city, the heavy-first gate proves nothing "
            + "here");
    }


    /**
     * B2.2 in the words the acceptance criterion uses: no branch is popped while a
     * heavier candidate is still waiting.
     *
     * Strictly implied by the invariant above, and written out anyway because it is the
     * property the lift depends on, and because the branch count assertion is what says
     * the city under test actually contains branches to be held back.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ABranchIsNeverPoppedWhileAHeavierCandidateWaits(string idString, float size)
    {
        var pops = _drainOf(idString, size, heavyFirst: true);
        var branches = pops.Where(p => _isBranch(p.Candidate)).ToList();

        Assert.True(branches.Count > 5,
            $"{_key(idString, size)}: only {branches.Count} branch candidates were judged "
            + "at all, so this city cannot show that branches wait");

        Assert.DoesNotContain(branches, p => p.HeaviestLeft > p.Candidate.Weight);
    }


    /**
     * And the ordering reaches the OUTPUT, not just the hook.
     *
     * A pop observer wired to a queue that the acceptance loop then ignores would satisfy
     * everything above. It cannot satisfy this: the two orderings accept different
     * candidates and therefore build different cities.
     *
     * Yelukhdidru@100 is excluded because it generates nothing at all (its corner seeds
     * are outside its own bounds), and a city with no strokes is identical under any
     * ordering - which is a fact about that seed, not about the queue.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheOrderingChangesTheCityThatComesOut(string idString, float size)
    {
        string off = StreetNetworkFingerprint.V2(StreetHarness.Generate(idString, size));
        string on = StreetNetworkFingerprint.V2(StreetHarness.GenerateHeavyFirst(idString, size));

        Assert.True(off != on,
            $"{_key(idString, size)}: draining the queue heaviest first produced the "
            + $"identical network ({off}), so the ordering is not reaching the accept "
            + "loop at all");
    }


    /**
     * ⚠️ A GENERATOR WHOSE BUDGET ACTUALLY BINDS, WHICH NO PINNED SEED DOES.
     *
     * StreetDeterminismTests has claimed since it was written that Yelukhdidru@3000
     * "exercises the maxGenerations = Size^2/1000 budget cut-off". Measured: its
     * _generationCounter finishes at 1886 against a budget of 9000, and every other
     * pinned seed is further below its own (seed017@2400: 1034 of 5760; seed000@1500:
     * 365 of 2250). Every one of the eight leaves by the queue running dry, so the
     * budget exit - and, until this work package, its own copy of the connect pass -
     * was reached by no test and no recorded city at all.
     *
     * The budget is Size^2/1000 while the area streets may grow in comes from
     * SetBounds, and the two are independent. So: a 200 m cluster, hence a budget of
     * 40, growing inside a 2 km square.
     */
    private static Generator _generatorWhoseBudgetBinds(
        StrokeStore store, bool heavyFirst, params (float x, float y)[] seedsAt)
    {
        var cd = StreetHarness.MakeCluster("budget-bound", 200f);

        var g = new Generator();
        g.SetAnnotation("budget-bound");
        g.Reset("streets-budget-bound", store, cd);
        g.EnableGradeSeparation = heavyFirst;
        g.SetBounds(-1000f, -1000f, 1000f, 1000f);

        foreach (var (x, y) in seedsAt)
        {
            var a = new StreetPoint() { ClusterId = 0 };
            a.SetPos(x, y);
            g.AddStartingStroke(Stroke.CreateByAngleFrom(
                cd, a, new StreetPoint() { ClusterId = 0 }, 0f, 90f, true, 1.1f));
        }

        return g;
    }


    private static void _commit(StrokeStore store, float x0, float y0, float x1, float y1)
    {
        var a = new StreetPoint() { ClusterId = 0 };
        a.SetPos(x0, y0);
        var b = new StreetPoint() { ClusterId = 0 };
        b.SetPos(x1, y1);

        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Street
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);
    }


    private static int _counterOf(Generator g)
        => (int)typeof(Generator)
            .GetField("_generationCounter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(g)!;


    /**
     * B2.6: the generation budget is one allowance for the whole run, not one per pass
     * over the queue.
     *
     * Driven rather than described, on the fixture above, because a second Generate() is
     * the shape a tiered ordering would take. With the budget already spent it must add
     * nothing. The positive control is the second half: hand the run a fresh allowance
     * and the same call DOES keep building, which is what says the first assertion is
     * about the budget and not about an exhausted queue.
     */
    [Fact]
    public void TheBudgetIsSpentOncePerRunAndNotPerPassOverTheQueue()
    {
        var store = new StrokeStore(3000f);
        var g = _generatorWhoseBudgetBinds(store, heavyFirst: true, (-400f, 0f));
        g.Generate();

        int afterFirst = store.GetStrokes().Count;

        Assert.True(_counterOf(g) > 40,
            $"the fixture finished at {_counterOf(g)} generations without reaching its "
            + "budget of 40, so it does not exercise the budget exit at all");
        Assert.True(afterFirst > 10, $"the fixture built only {afterFirst} strokes");

        g.Generate();
        Assert.Equal(afterFirst, store.GetStrokes().Count);

        typeof(Generator)
            .GetField("_generationCounter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(g, 0);

        g.Generate();
        Assert.True(store.GetStrokes().Count > afterFirst,
            "with the counter reset the run built nothing more, so the budget was not "
            + "what stopped it and this test says nothing about budgeting");
    }


    /**
     * B2.5, behavioural half, on the exit that had never been driven: a run cut off by
     * its budget still gets its orphans bridged.
     *
     * The connect pass used to be called from each of the two returns. Hoisting it to a
     * single call after _drain() is only correct if neither exit lost it, and the budget
     * exit's copy was reached by nothing - so deleting the hoisted call and putting one
     * back on the queue-empty exit alone would have passed every gate in this repository.
     *
     * Two strokes already in the store, in opposite corners 2.5 km apart, and a candidate
     * seed in the middle. A budget of 40 cannot grow from the middle to either corner, so
     * the single component this asserts can only have come from bridging.
     */
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARunCutOffByItsBudgetStillGetsItsOrphansBridged(bool heavyFirst)
    {
        var store = new StrokeStore(3000f);
        _commit(store, -900f, -900f, -800f, -900f);
        _commit(store, 900f, 900f, 800f, 900f);

        var g = _generatorWhoseBudgetBinds(store, heavyFirst, (0f, 0f));
        g.Generate();

        Assert.True(_counterOf(g) > 40,
            $"the fixture left by the queue running dry at {_counterOf(g)} generations, "
            + "not by its budget of 40");

        Assert.Contains(store.GetStrokes(), s => s.Kind == StrokeKind.ConnectorBridge);
        Assert.Equal(1, StreetHarness.CountComponents(store));
    }


    /**
     * And the exit every recorded city actually takes: the queue runs dry, and orphan
     * bridging still runs there too.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("seed011", 500f)]
    [InlineData("Yelukhdidru", 3000f)]
    public void OrphanBridgingStillRunsWhenTheQueueRunsDry(string idString, float size)
    {
        Assert.Equal(1, StreetHarness.CountComponents(StreetHarness.Generate(idString, size)));
        Assert.Equal(1, StreetHarness.CountComponents(
            StreetHarness.GenerateHeavyFirst(idString, size)));
    }


    /**
     * B2.5, structural half: ONE call site.
     *
     * A scan, and deliberately labelled as one, because running the connect pass twice is
     * not behaviourally observable: the second run finds a single component and returns
     * before it touches the RandomSource. What the hoist buys is a place to stand between
     * the drain and the bridging, and only the source can say that place exists.
     */
    [Fact]
    public void TheConnectPassIsCalledExactlyOnceAndAfterTheDrain()
    {
        string source = _sourceOf("Generator.cs");

        Assert.Equal(1, source.Split("_connectPass.Run()").Length - 1);
        Assert.Equal(1, source.Split("_drain();").Length - 1);
        Assert.Equal(1, source.Split("private void _drain()").Length - 1);

        int drain = source.IndexOf("_drain();", StringComparison.Ordinal);
        int connect = source.IndexOf("_connectPass.Run();", StringComparison.Ordinal);

        Assert.True(drain > 0 && connect > drain,
            "the connect pass must be called after _drain() returns, in the same method");

        /*
         * And it is not back inside the loop: no return statement stands between them.
         */
        Assert.DoesNotContain("return", source.Substring(drain, connect - drain));
    }


    /**
     * The queue's own contract, the WEAKER half of B2.1.
     *
     * Flag off, Pop is the last element and nothing else. What actually says the default
     * city did not move is the eight recorded fingerprints, street-geometry.json and
     * StreetCostTests; this only says what the structure does when asked directly.
     */
    [Fact]
    public void TheFlagOffQueueIsAPlainStack()
    {
        var q = new CandidateQueue();
        var pushed = new List<Stroke>();

        foreach (float w in new[] { 0.2f, 1.3f, 0.7f, 1.3f, 0.4f })
        {
            var s = new Stroke() { ClusterId = 0, Weight = w };
            pushed.Add(s);
            q.Push(s);
        }

        for (int i = pushed.Count - 1; i >= 0; --i)
        {
            Assert.Same(pushed[i], q.Pop());
        }

        Assert.Equal(0, q.Count);
    }


    /**
     * Heavy first, and the tie break that keeps a split's two halves in the order the
     * split pushed them: among equal weights the most recently pushed wins, so the queue
     * is still a stack WITHIN one weight.
     *
     * Asserted by identity on the two 1.3 f entries, not by weight - a comparison on
     * weight cannot tell the two apart, which is the whole point.
     */
    [Fact]
    public void HeavyFirstBreaksTiesTheWayTheStackWould()
    {
        var q = new CandidateQueue { HeavyFirst = true };
        var pushed = new List<Stroke>();

        foreach (float w in new[] { 0.2f, 1.3f, 0.7f, 1.3f, 0.4f })
        {
            var s = new Stroke() { ClusterId = 0, Weight = w };
            pushed.Add(s);
            q.Push(s);
        }

        Assert.Same(pushed[3], q.Pop());
        Assert.Same(pushed[1], q.Pop());
        Assert.Same(pushed[2], q.Pop());
        Assert.Same(pushed[4], q.Pop());
        Assert.Same(pushed[0], q.Pop());
    }


    private static string _sourceOf(string fileName)
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return System.IO.File.ReadAllText(
            System.IO.Path.Combine(root, "engine", "streets", fileName));
    }
}
