using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The middle junction of a long bridge corridor.
 *
 * ConnectComponentsPass._createBridgeCorridor computed the mid position - the
 * RandomSource draw for its perpendicular offset included - and never assigned it, so
 * the junction stayed where a fresh StreetPoint starts: the CLUSTER ORIGIN. Nothing
 * failed; the graph was connected, both halves were ordinary ConnectorBridge strokes,
 * and the only symptom was two enormous roads through the middle of the city.
 *
 * WHAT THE ASSERTION HAS TO BE. "The mid junction is not at the origin" is the shape of
 * the symptom, not the property, and it fails both ways: a corridor whose two ends
 * genuinely straddle the origin has its mid there legitimately, and the one generated
 * city that reaches this branch happens to have its corridor far from the origin, so an
 * origin check would have passed there for no reason connected to the defect. The
 * property is that the mid stands BETWEEN the two ends - its projection onto the chord
 * is the chord's own midpoint - and OFF the chord by the offset that was drawn for it.
 * Both halves are needed: the origin fixture below satisfies the first on its own while
 * the offset is still being thrown away.
 */
public class ConnectComponentsCorridorTests
{
    private const float ClusterSize = 4000f;

    /*
     * ConnectComponentsPass draws `40f + rnd.GetFloat() * 40f`.
     */
    private const float MinOffset = 40f;
    private const float MaxOffset = 80f;

    /*
     * StreetPoint.SetPos truncates to a tenth of a metre, and the ends were placed
     * through it too, so three truncations can accumulate.
     */
    private const float Quantum = 0.35f;


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    private static void _addPolyline(StrokeStore store, params (float x, float y)[] pts)
    {
        var points = pts.Select(p => _pointAt(p.x, p.y)).ToList();

        for (int i = 0; i + 1 < points.Count; ++i)
        {
            var s = new Stroke()
            {
                ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0,
                Kind = StrokeKind.Street
            };
            s.A = points[i];
            s.B = points[i + 1];
            store.AddStroke(s);
        }
    }


    private static void _run(StrokeStore store)
    {
        new ConnectComponentsPass(
            store, new NetworkBuilder(store), 0, new RandomSource("corridor"), "test").Run();
    }


    /**
     * The corridor's own middle junction, by the tag the pass gives it rather than by
     * position - looking for a point near where it ought to be is exactly the assertion
     * that cannot tell a correct answer from a refusal.
     */
    private static StreetPoint _midOf(StrokeStore store)
    {
        var mids = store.GetStreetPoints()
            .Where(p => p.Creator.Contains("corridor_mid")).ToList();

        Assert.True(1 == mids.Count,
            $"expected exactly one corridor middle junction, found {mids.Count}; without "
            + "one this fixture says nothing about the corridor branch at all");

        return mids[0];
    }


    /**
     * The two junctions the corridor runs between: the far ends of the two halves.
     */
    private static (StreetPoint from, StreetPoint to, Stroke first, Stroke second)
        _endsOf(StrokeStore store, StreetPoint mid)
    {
        var halves = store.GetStrokes().Where(s => s.A == mid || s.B == mid).ToList();
        Assert.Equal(2, halves.Count);

        foreach (var h in halves)
        {
            Assert.Equal(StrokeKind.ConnectorBridge, h.Kind);
        }

        return (halves[0].A == mid ? halves[0].B : halves[0].A,
                halves[1].A == mid ? halves[1].B : halves[1].A,
                halves[0], halves[1]);
    }


    /**
     * The property, measured in the chord's own frame: how far along the chord the mid
     * stands, and how far off it.
     */
    private static (float along, float across, float chord) _placementOf(
        StreetPoint from, StreetPoint to, StreetPoint mid)
    {
        Vector2 d = to.Pos - from.Pos;
        float chord = d.Length();
        Vector2 unit = d / chord;
        Vector2 rel = mid.Pos - from.Pos;

        return (Vector2.Dot(rel, unit),
                Single.Abs(rel.X * -unit.Y + rel.Y * unit.X),
                chord);
    }


    private static void _assertMidIsBetweenItsEnds(StrokeStore store, string what)
    {
        var mid = _midOf(store);
        var (from, to, first, second) = _endsOf(store, mid);
        var (along, across, chord) = _placementOf(from, to, mid);

        Assert.True(chord > 300f,
            $"{what}: the two ends are {chord:F1} m apart, which does not reach the "
            + "corridor branch (>300 m) - this fixture would prove nothing");

        Assert.True(Single.Abs(along - 0.5f * chord) <= Quantum,
            $"{what}: the mid junction at {mid.Pos} stands {along:F1} m along a chord of "
            + $"{chord:F1} m from {from.Pos} to {to.Pos}; it belongs at its midpoint, "
            + $"{0.5f * chord:F1} m");

        Assert.True(across >= MinOffset - Quantum && across <= MaxOffset + Quantum,
            $"{what}: the mid junction stands {across:F2} m off the chord; the pass draws "
            + $"an offset in [{MinOffset}, {MaxOffset}) and this is what says the draw is "
            + "used rather than merely made");

        /*
         * The consequence, stated because it is what a player sees: two halves of equal
         * length, together barely longer than the gap they bridge.
         */
        Assert.True(Single.Abs(first.Length - second.Length) <= 2f * Quantum,
            $"{what}: the two halves are {first.Length:F1} m and {second.Length:F1} m");

        Assert.True(first.Length + second.Length < 1.2f * chord,
            $"{what}: the corridor is {first.Length + second.Length:F1} m long for a "
            + $"{chord:F1} m gap");
    }


    /**
     * The fixture the fix needs: two components far enough apart to reach the corridor
     * branch, and nowhere near the cluster origin.
     *
     * With the assignment missing the mid lands at (0,0), which is 1.1 km off this
     * chord; every one of the assertions above fails.
     */
    [Fact]
    public void TheCorridorMidJunctionStandsBetweenItsTwoEnds()
    {
        var store = new StrokeStore(ClusterSize);

        _addPolyline(store, (-1200f, 900f), (-1000f, 900f));
        _addPolyline(store, (-600f, 700f), (-400f, 700f));

        _run(store);

        _assertMidIsBetweenItsEnds(store, "off-origin corridor");
    }


    /**
     * The same branch with the chord's midpoint sitting exactly ON the cluster origin.
     *
     * This is the fixture that says what the assertion has to be. With the assignment
     * missing the mid lands at (0,0), which IS the chord's midpoint - so "the junction
     * stands between its two ends" is satisfied by the defect here, and only the
     * perpendicular offset distinguishes the drawn position from the default one. A
     * check on the distance to the origin is worse than useless in this geometry: it
     * would reject the correct answer as readily as the wrong one.
     */
    [Fact]
    public void TheOffsetIsWhatSeparatesADrawnMidFromADefaultOne()
    {
        var store = new StrokeStore(ClusterSize);

        _addPolyline(store, (-400f, 0f), (-200f, 0f));
        _addPolyline(store, (200f, 0f), (400f, 0f));

        _run(store);

        var mid = _midOf(store);
        var (from, to, _, _) = _endsOf(store, mid);
        var (along, across, chord) = _placementOf(from, to, mid);

        Assert.True(Single.Abs(along - 0.5f * chord) <= Quantum,
            "the fixture is supposed to put the chord's midpoint on the origin, so that "
            + "the along-chord half of the property cannot distinguish anything here");

        Assert.True(across >= MinOffset - Quantum,
            $"the mid junction stands {across:F2} m off a chord whose midpoint is the "
            + "cluster origin; without the offset it is indistinguishable from a "
            + "StreetPoint that was never given a position at all");

        _assertMidIsBetweenItsEnds(store, "on-origin corridor");
    }


    /**
     * And the one generated city in 180 that reaches this branch at all.
     *
     * seed017@2400 is pinned by StreetDeterminismTests for exactly this reason. A 318 m
     * gap used to be bridged by 1341.7 m + 1050.3 m through the middle of the city;
     * it is now 165.4 m + 165.3 m.
     *
     * The count assertion is not decoration: if this seed ever stops reaching the
     * corridor branch the rest of this test passes vacuously, and the branch goes back to
     * being covered by nothing.
     */
    [Fact]
    public void TheOneGeneratedCityThatBuildsACorridorBuildsItBetweenItsEnds()
    {
        var store = StreetHarness.Generate("seed017", 2400f);

        Assert.Equal(2, store.GetStrokes().Count(
            s => s.Creator.Contains("corridor_seg1") || s.Creator.Contains("corridor_seg2")));

        _assertMidIsBetweenItsEnds(store, "seed017@2400");
    }


    /**
     * No other pinned seed reaches the branch, which is what makes seed017@2400 load
     * bearing. Recorded here so that a ruleset change that quietly stops exercising it
     * is visible.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("seed011", 500f)]
    [InlineData("seed000", 1500f)]
    [InlineData("Yelukhdidru", 400f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("Yelukhdidru", 3000f)]
    public void NoOtherPinnedSeedReachesTheCorridorBranch(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);

        Assert.DoesNotContain(store.GetStreetPoints(), p => p.Creator.Contains("corridor_mid"));
    }
}
