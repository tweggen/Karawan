using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Cost parity gate.
 *
 * The rework is only acceptable if it is at most as expensive as what it replaces.
 * Allocated bytes is the primary metric because it is essentially deterministic,
 * unlike wall time on a shared CI box. GC.GetAllocatedBytesForCurrentThread is used
 * rather than GC.GetTotalAllocatedBytes precisely because the suite runs in parallel:
 * a process-wide counter would pick up every other test's allocations.
 *
 * Wall time is recorded alongside for information, and deliberately NOT asserted on.
 */
public class StreetCostTests
{
    private const string BaselineFile = "street-cost-baseline.json";

    /*
     * Generous enough to absorb collection-growth boundaries, tight enough that a
     * regression of any real size trips it.
     */
    private const double ToleranceFraction = 0.02;

    /*
     * Allocation is very nearly, but not exactly, reproducible: repeated generation of
     * seed011@500 was measured at 74944 and then 74960 bytes, a 16 byte (0.02%) drift
     * with a byte-identical resulting network. Runtime-internal lazy state, not the
     * generator. Measuring the minimum of several runs removes most of it - noise only
     * ever adds allocations - and this tolerance absorbs the rest.
     */
    private const double ReproducibilityTolerance = 0.005;

    private const int MeasurementRepeats = 3;

    /*
     * A relative tolerance alone is the wrong shape for the degenerate seeds. The
     * empty Yelukhdidru@100 network allocates ~6.5 KB in total, so runtime noise of
     * 152 bytes reads as +2.4% and trips a 2% ceiling, while the same 152 bytes on the
     * 4.9 MB seed is 0.003%. Observed for real: the suite composition changed when
     * baked assets became available and that seed drifted 6464 -> 6616 bytes, with
     * SplitStrokeAt never once called because the network has no strokes at all.
     *
     * The absolute slack below floors the allowance. On the largest seed it is 0.17%,
     * so the relative check still dominates wherever the cost signal actually lives.
     */
    private const long AbsoluteSlackBytes = 8192;


    public static IEnumerable<object[]> Seeds => StreetDeterminismTests.Seeds;


    private static string _key(string idString, float size)
        => $"{idString}@{size:F0}";


    private static long _measureAllocations(string idString, float size)
    {
        /*
         * Warm up first: the very first generation in a process pays JIT and static
         * constructor costs that have nothing to do with the generator itself.
         */
        StreetHarness.Generate(idString, size);

        long best = long.MaxValue;
        for (int i = 0; i < MeasurementRepeats; ++i)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            StreetHarness.Generate(idString, size);
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            if (delta < best) best = delta;
        }

        return best;
    }


    /**
     * Allocation counts must themselves be reproducible, otherwise they are useless
     * as a gate.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AllocationIsReproducible(string idString, float size)
    {
        long first = _measureAllocations(idString, size);
        long second = _measureAllocations(idString, size);

        long allowed = (long)(first * ReproducibilityTolerance) + AbsoluteSlackBytes;

        Assert.True(Math.Abs(second - first) <= allowed, string.Format(CultureInfo.InvariantCulture,
            "{0}: allocation is not reproducible within a process ({1:N0} vs {2:N0} bytes, " +
            "allowed drift {3:N0}).",
            _key(idString, size), first, second, allowed));
    }


    [Theory]
    [MemberData(nameof(Seeds))]
    public void AllocationDoesNotRegress(string idString, float size)
    {
        string stamp = StreetNetworkFingerprint.EnvironmentStamp();
        string key = _key(idString, size);

        long allocated = _measureAllocations(idString, size);

        var sw = Stopwatch.StartNew();
        StreetHarness.Generate(idString, size);
        sw.Stop();

        if (StreetBaselines.WriteRequested)
        {
            var root = StreetBaselines.Load(BaselineFile);
            StreetBaselines.Record(root, stamp, key, new JsonObject
            {
                ["allocatedBytes"] = JsonValue.Create(allocated),
                ["wallMsAdvisory"] = JsonValue.Create(sw.Elapsed.TotalMilliseconds)
            });
            StreetBaselines.Save(BaselineFile, root);
            return;
        }

        var baselines = StreetBaselines.EntriesFor(StreetBaselines.Load(BaselineFile), stamp);
        Assert.True(baselines != null,
            StreetBaselines.MissingBaselineMessage(stamp, BaselineFile));

        Assert.True(baselines!.TryGetPropertyValue(key, out var entry) && entry != null,
            $"No cost baseline for seed '{key}' under environment '{stamp}'. " +
            $"Observed {allocated} bytes.");

        long expected = entry!.AsObject()["allocatedBytes"]!.GetValue<long>();
        long ceiling = (long)(expected * (1.0 + ToleranceFraction)) + AbsoluteSlackBytes;

        Assert.True(allocated <= ceiling, string.Format(CultureInfo.InvariantCulture,
            "{0}: allocation regressed from {1:N0} to {2:N0} bytes (+{3:P1}, ceiling {4:N0}).",
            key, expected, allocated, (allocated - expected) / (double)expected, ceiling));
    }
}
