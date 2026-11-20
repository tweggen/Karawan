using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.elevation;
using engine.world;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.world;


/**
 * KAR-484: the groove an intercity line carves is centred on its station axis.
 *
 * Reported: the highway assist's line ran on one side of the groove. Measured over the
 * shipped world's 114 lines, cross-sections every 10 m, the carved band's centre was off
 * the axis by 8.6 m at the median, 18.6 m p90, 35.9 m worst. IntercityTrackElevationOperator
 * placed segment sample tex at extent x tex / nHoriz, but a segment has GroundResolution + 1
 * samples over its extent, so every sample was taken to be up to a grid step short of where
 * it is, restarting at every fragment. The operator now asks the segment
 * (ElevationSegment.SampleX/Z) and carves by IntercityCorridor.IsInCarvedBand - both used
 * here exactly as the operator uses them.
 *
 * Then widened: the terrain between samples is interpolated, so the flat floor is the
 * carved band less up to a cell diagonal. Carved two grid steps either side it was as
 * narrow as 12 m beside the axis; carved three it is at least 31.5 m, and the operator now
 * runs on every fragment the band reaches, not only those the axis crosses.
 */
public class IntercityGrooveTests
{
    private readonly ITestOutputHelper _out;
    public IntercityGrooveTests(ITestOutputHelper output) => _out = output;

    private static float GridStep => MetaGen.FragmentSize / MetaGen.GroundResolution;


    /** A fragment's segment, the way elevation.Cache builds one: n + 1 samples over it. */
    private static ElevationSegment _fragmentSegment(float x0, float z0)
    {
        var es = new ElevationSegment(MetaGen.GroundResolution + 1, MetaGen.GroundResolution + 1);
        es.Rect2.A.X = x0;
        es.Rect2.A.Y = z0;
        es.Rect2.B.X = x0 + MetaGen.FragmentSize;
        es.Rect2.B.Y = z0 + MetaGen.FragmentSize;
        return es;
    }


    [Fact]
    public void ASegmentsSamplesRunFromEdgeToEdge()
    {
        var es = _fragmentSegment(-200f, 600f);
        Assert.Equal(20f, es.StepX);
        Assert.Equal(-200f, es.SampleX(0));
        Assert.Equal(200f, es.SampleX(MetaGen.GroundResolution));
        Assert.Equal(1000f, es.SampleZ(MetaGen.GroundResolution));
    }


    private sealed record Groove(Vector2 A, Vector2 B, HashSet<Vector2> Carved);

    private static List<Groove> _grooves;
    private static readonly object _lo = new();


    /**
     * Every sample the operator flattens for each shipped line: in the fragments it now
     * selects (IntercityCorridor.BandTouches), at the positions the segment gives, by the
     * shared band rule - each exactly as the operator calls it.
     */
    private static List<Groove> _shippedGrooves()
    {
        lock (_lo)
        {
            if (_grooves != null) return _grooves;
            float fs = MetaGen.FragmentSize;
            var result = new List<Groove>();
            foreach (var l in IntercityWorldHarness.Lines())
            {
                var a = new Vector2(l.PosA.X, l.PosA.Z);
                var b = new Vector2(l.PosB.X, l.PosB.Z);
                var axis = Vector2.Normalize(b - a);
                float length = (b - a).Length();

                var fragments = new HashSet<(int, int)>();
                for (float s = 0; s <= length; s += fs / 8)
                {
                    var p = a + axis * s;
                    int i = (int)MathF.Floor((p.X + fs / 2) / fs), k = (int)MathF.Floor((p.Y + fs / 2) / fs);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        fragments.Add((i + dx, k + dz));
                }

                var carved = new HashSet<Vector2>();
                foreach (var (i, k) in fragments)
                {
                    var es = _fragmentSegment(i * fs - fs / 2, k * fs - fs / 2);
                    if (!IntercityCorridor.BandTouches(
                            new Vector2(es.Rect2.A.X, es.Rect2.A.Y), new Vector2(es.Rect2.B.X, es.Rect2.B.Y), a, b))
                    {
                        continue;
                    }
                    for (int tez = 0; tez < es.nVert; tez++)
                    for (int tex = 0; tex < es.nHoriz; tex++)
                    {
                        var sample = new Vector2(es.SampleX(tex), es.SampleZ(tez));
                        if (IntercityCorridor.IsInCarvedBand(sample, a, b)) carved.Add(sample);
                    }
                }
                result.Add(new Groove(a, b, carved));
            }
            return _grooves = result;
        }
    }


    /**
     * Flat ground as the terrain sees it: CacheEntry interpolates over two triangles per cell,
     * split along its anti-diagonal, so a point is at the track's height only where all three
     * corners of its triangle were flattened.
     */
    private static bool _isFlat(Vector2 p, HashSet<Vector2> carved)
    {
        float s = GridStep;
        float fx = MathF.Floor(p.X / s) * s, fz = MathF.Floor(p.Y / s) * s;
        bool upper = (p.X - fx) + (p.Y - fz) <= s;
        var c01 = new Vector2(fx + s, fz);
        var c10 = new Vector2(fx, fz + s);
        var c0 = upper ? new Vector2(fx, fz) : new Vector2(fx + s, fz + s);
        return carved.Contains(c0) && carved.Contains(c01) && carved.Contains(c10);
    }


    [Fact]
    public void TheShippedGroovesAreCentredOnTheirAxes()
    {
        var offsets = new List<float>();
        foreach (var g in _shippedGrooves())
        {
            var axis = Vector2.Normalize(g.B - g.A);
            var right = IntercityCorridor.RightOf(axis);
            float length = (g.B - g.A).Length();
            for (float s = 200; s < length - 200; s += 10)
            {
                var c = g.A + axis * s;
                var lateral = g.Carved.Where(v => MathF.Abs(Vector2.Dot(v - c, axis)) <= GridStep / 2f)
                    .Select(v => Vector2.Dot(v - g.A, right)).ToList();
                Assert.NotEmpty(lateral);
                offsets.Add((lateral.Max() + lateral.Min()) / 2f);
            }
        }

        var abs = offsets.Select(MathF.Abs).OrderBy(x => x).ToList();
        float P(double q) => abs[(int)Math.Min(abs.Count - 1, q * abs.Count)];
        float signedMedian = offsets.OrderBy(x => x).ElementAt(offsets.Count / 2);
        _out.WriteLine($"{offsets.Count} cross-sections: groove centre off its axis p50 {P(.5):F1} p90 {P(.9):F1} max {abs[^1]:F1} m; signed median {signedMedian:F2}");

        // What is left is the grid itself; the shipped mapping was 8.6 / 18.6 / 35.9 m.
        Assert.True(P(.5) < 5f, $"median offset {P(.5)} m");
        Assert.True(abs[^1] <= GridStep, $"worst offset {abs[^1]} m");
        Assert.True(MathF.Abs(signedMedian) < 1f, "the groove leans to one side");
    }


    /**
     * The corridor is flat ground: over every shipped line, every point within
     * IntercityCorridor.HalfWidth of the axis is on a triangle the operator flattened.
     * Measured before widening (two grid steps carved): as little as 12 m either side.
     */
    [Fact]
    public void TheCorridorIsFlatGroundForItsWholeWidth()
    {
        float half = IntercityCorridor.HalfWidth;
        float narrowest = Single.MaxValue;
        int sections = 0;
        foreach (var g in _shippedGrooves())
        {
            var axis = Vector2.Normalize(g.B - g.A);
            var right = IntercityCorridor.RightOf(axis);
            float length = (g.B - g.A).Length();
            for (float s = 0; s <= length; s += 10)
            {
                var c = g.A + axis * s;
                ++sections;
                for (float d = -half; d <= half; d += 0.5f)
                {
                    Assert.True(_isFlat(c + right * d, g.Carved), $"not flat {d:F1} m off the axis at {c}");
                }
                float edge = 0f;
                while (edge < 100f && _isFlat(c + right * (edge + 0.5f), g.Carved)
                                   && _isFlat(c - right * (edge + 0.5f), g.Carved)) edge += 0.5f;
                narrowest = MathF.Min(narrowest, edge);
            }
        }
        _out.WriteLine($"{sections} cross-sections: flat for at least {narrowest:F1} m either side (corridor {half:F1} m)");
    }


    /**
     * The operator runs on every fragment that holds band between the stations - the
     * neighbour of an axis near a fragment border included.
     */
    [Fact]
    public void EveryBandSampleBetweenTheStationsIsCarved()
    {
        int missing = 0, total = 0;
        foreach (var g in _shippedGrooves())
        {
            var axis = Vector2.Normalize(g.B - g.A);
            var right = IntercityCorridor.RightOf(axis);
            float length = (g.B - g.A).Length();
            float s0 = GridStep;
            var lo = Vector2.Min(g.A, g.B) - new Vector2(IntercityCorridor.CarveHalfWidth);
            var hi = Vector2.Max(g.A, g.B) + new Vector2(IntercityCorridor.CarveHalfWidth);
            for (float x = MathF.Floor(lo.X / s0) * s0; x <= hi.X; x += s0)
            for (float z = MathF.Floor(lo.Y / s0) * s0; z <= hi.Y; z += s0)
            {
                var v = new Vector2(x, z);
                float along = Vector2.Dot(v - g.A, axis);
                if (along < 0 || along > length) continue;
                if (MathF.Abs(Vector2.Dot(v - g.A, right)) > IntercityCorridor.CarveHalfWidth) continue;
                ++total;
                if (!g.Carved.Contains(v)) ++missing;
            }
        }
        _out.WriteLine($"{total} band samples between the stations, {missing} not carved");
        Assert.Equal(0, missing);
    }


    [Fact]
    public void TheOperatorCarvesWhereTheSegmentSaysItsSamplesAre()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string op = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/intercity/IntercityTrackElevationOperator.cs"));

        Assert.Contains("esTarget.SampleX(tex)", op);
        Assert.Contains("esTarget.SampleZ(tez)", op);
        Assert.Contains("IntercityCorridor.IsInCarvedBand(", op);
        Assert.Contains("IntercityCorridor.BandTouches(", op);
        Assert.DoesNotContain("_line.Width", op);
        Assert.DoesNotContain("/ esTarget.nHoriz", op);
        Assert.DoesNotContain("/ esTarget.nVert", op);
    }
}
