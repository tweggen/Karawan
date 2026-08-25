using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using engine.streets;

namespace JoyceCode.Tests.engine.streets;


/**
 * A stable, comparable summary of a generated street network.
 *
 * Why not just serialize the network and compare bytes: StreetPoint._nextId and
 * Stroke._nextId are process-global static counters, so the IDs a cluster gets depend
 * on what else allocated points earlier in the same process. Any fingerprint that
 * includes IDs is order-dependent and therefore useless under a parallel test runner.
 * This one hashes geometry only.
 *
 * V1 deliberately omits stroke Level. It is the ground-only equivalence gate and must
 * keep working unchanged once multilayer lands in WP-4, where a V2 that includes Level
 * is added alongside it.
 */
internal static class StreetNetworkFingerprint
{
    /**
     * Canonical, ID-independent, order-independent description of every stroke.
     */
    internal static string[] CanonicalLines(StrokeStore store)
    {
        var lines = new List<string>();

        foreach (var stroke in store.GetStrokes())
        {
            string a = _q(stroke.A.Pos);
            string b = _q(stroke.B.Pos);

            /*
             * Canonical endpoint order: which end a stroke calls A is an artefact of
             * how it was generated, not a property of the resulting network.
             */
            string p = string.CompareOrdinal(a, b) <= 0 ? a : b;
            string q = string.CompareOrdinal(a, b) <= 0 ? b : a;

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2:F3}|{3}", p, q, stroke.Weight, stroke.IsPrimary ? 1 : 0));
        }

        lines.Sort(StringComparer.Ordinal);
        return lines.ToArray();
    }


    /**
     * Compact fingerprint. Counts are carried in the clear so that a failure message
     * says how the network drifted before anyone opens a diff.
     */
    internal static string V1(StrokeStore store)
    {
        var lines = CanonicalLines(store);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

        return string.Format(CultureInfo.InvariantCulture,
            "n={0},s={1},h={2}",
            store.GetStreetPoints().Count,
            lines.Length,
            Convert.ToHexString(hash).Substring(0, 16));
    }


    /**
     * Human-readable difference between two networks, for when a gate fails.
     */
    internal static string Diff(string[] expected, string[] actual, int maxLines = 20)
    {
        var onlyExpected = expected.Except(actual, StringComparer.Ordinal).Take(maxLines).ToArray();
        var onlyActual = actual.Except(expected, StringComparer.Ordinal).Take(maxLines).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine($"strokes: expected {expected.Length}, actual {actual.Length}");
        sb.AppendLine($"-- only in expected ({onlyExpected.Length} shown) --");
        foreach (var line in onlyExpected) sb.AppendLine("  " + line);
        sb.AppendLine($"-- only in actual ({onlyActual.Length} shown) --");
        foreach (var line in onlyActual) sb.AppendLine("  " + line);
        return sb.ToString();
    }


    /**
     * Floating point results are only guaranteed to be reproducible for a given
     * runtime and architecture, so golden fingerprints are recorded per environment
     * rather than assumed to be portable.
     *
     * Deliberately major.minor rather than the full patch version: recompiling the
     * suite from net9.0 to net10.0 on the same runtime was verified to leave every
     * fingerprint bit-identical, so keying on the patch level would invalidate the
     * baselines on every routine runtime update for no benefit. A major runtime
     * change is the level at which codegen differences are at least conceivable.
     */
    internal static string EnvironmentStamp()
    {
        return string.Format(CultureInfo.InvariantCulture, ".NET {0}.{1}|{2}",
            Environment.Version.Major,
            Environment.Version.Minor,
            RuntimeInformation.ProcessArchitecture);
    }


    /*
     * 1 mm. A genuine behavioural change moves junctions by metres; this quantisation
     * only exists so that a last-bit difference cannot masquerade as one. A pure
     * refactor is expected to be bit-identical, so any mismatch is a real signal.
     */
    private static string _q(Vector2 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3}", v.X, v.Y);
    }
}
