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


    [Fact]
    public void TheShippedGroovesAreCentredOnTheirAxes()
    {
        float fs = MetaGen.FragmentSize;
        var offsets = new List<float>();
        var widths = new List<float>();

        foreach (var l in IntercityWorldHarness.Lines())
        {
            var a = new Vector2(l.PosA.X, l.PosA.Z);
            var b = new Vector2(l.PosB.X, l.PosB.Z);
            var axis = Vector2.Normalize(b - a);
            var right = IntercityCorridor.RightOf(axis);
            float length = (b - a).Length();

            // Every sample the operator flattens in the fragments along the line.
            var carved = new HashSet<Vector2>();
            var fragments = new HashSet<(float, float)>();
            for (float s = 0; s <= length; s += fs / 4)
            {
                var p = a + axis * s;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    fragments.Add((MathF.Floor((p.X + fs / 2) / fs) * fs + dx * fs - fs / 2,
                                   MathF.Floor((p.Y + fs / 2) / fs) * fs + dz * fs - fs / 2));
                }
            }
            foreach (var (x0, z0) in fragments)
            {
                var es = _fragmentSegment(x0, z0);
                for (int tez = 0; tez < es.nVert; tez++)
                for (int tex = 0; tex < es.nHoriz; tex++)
                {
                    var sample = new Vector2(es.SampleX(tex), es.SampleZ(tez));
                    if (IntercityCorridor.IsInCarvedBand(sample, a, b, GridStep)) carved.Add(sample);
                }
            }

            for (float s = 200; s < length - 200; s += 10)
            {
                var c = a + axis * s;
                var lateral = carved.Where(v => MathF.Abs(Vector2.Dot(v - c, axis)) <= GridStep / 2f)
                    .Select(v => Vector2.Dot(v - a, right)).ToList();
                Assert.NotEmpty(lateral);
                offsets.Add((lateral.Max() + lateral.Min()) / 2f);
                widths.Add(lateral.Max() - lateral.Min());
                Assert.True(lateral.Min() <= 0f && lateral.Max() >= 0f, $"the axis is outside the groove at {c}");
            }
        }

        var abs = offsets.Select(MathF.Abs).OrderBy(x => x).ToList();
        widths.Sort();
        float P(List<float> v, double q) => v[(int)Math.Min(v.Count - 1, q * v.Count)];
        _out.WriteLine($"{offsets.Count} cross-sections: groove centre off its axis p50 {P(abs, .5):F1} p90 {P(abs, .9):F1} " +
                       $"max {abs[^1]:F1} m; width p10 {P(widths, .1):F1} p50 {P(widths, .5):F1} m; signed median {offsets.OrderBy(x => x).ElementAt(offsets.Count / 2):F2}");

        // What is left is the 20 m grid itself; the shipped mapping was 8.6 / 18.6 / 35.9 m.
        Assert.True(P(abs, .5) < 5f, $"median offset {P(abs, .5)} m");
        Assert.True(abs[^1] <= GridStep, $"worst offset {abs[^1]} m");
        Assert.True(MathF.Abs(offsets.OrderBy(x => x).ElementAt(offsets.Count / 2)) < 1f, "the groove leans to one side");
    }


    [Fact]
    public void TheOperatorCarvesWhereTheSegmentSaysItsSamplesAre()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string op = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/intercity/IntercityTrackElevationOperator.cs"));

        Assert.Contains("esTarget.SampleX(tex)", op);
        Assert.Contains("esTarget.SampleZ(tez)", op);
        Assert.Contains("IntercityCorridor.IsInCarvedBand(", op);
        Assert.DoesNotContain("/ esTarget.nHoriz", op);
        Assert.DoesNotContain("/ esTarget.nVert", op);
    }
}
