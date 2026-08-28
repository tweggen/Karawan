using System;
using System.IO;
using System.Linq;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * One stroke, one fragment.
 *
 * GenerateClusterStreetsOperator runs once per fragment overlapping a cluster, but walks
 * the WHOLE cluster's stroke store each time. Everything it emits per stroke therefore
 * needs the same guard - "only if this stroke's A point is in this fragment" - or a
 * stroke spanning two fragments is emitted twice, and a stroke in a far corner of the
 * city is emitted once per loaded fragment.
 *
 * The mesh loop has had that guard from the beginning. The collider loop was written
 * without it and nobody noticed, because it only ever emitted anything for raised decks
 * and no shipped ruleset makes any. It became a pile of duplicate statics the moment
 * ordinary streets needed colliders.
 *
 * A source scan, because the thing being checked is a rule about how the file is
 * written, and the loops it applies to need a fragment and a physics simulation to run.
 */
public class StreetFragmentOwnershipTests
{
    private const string Guard = "IsInsideLocal";
    private const string StrokeLoop = "foreach (var stroke in strokeStore.GetStrokes())";

    /**
     * How far after the loop header the guard may appear. Generous, since a comment
     * explaining the guard sits between them.
     */
    private const int GuardWithinLines = 22;


    private static string _operatorSource()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/streets/GenerateClusterStreetsOperator.cs";

        Assert.True(File.Exists(path), $"could not find the operator source at {path}");

        return path;
    }


    [Fact]
    public void EveryPerStrokeLoopIsFilteredToItsOwnFragment()
    {
        string[] lines = File.ReadAllLines(_operatorSource());

        var loops = Enumerable.Range(0, lines.Length)
            .Where(i => lines[i].Contains(StrokeLoop))
            .ToList();

        /*
         * Two today - the mesh and the colliders. If this drops to one the scan has
         * stopped finding what it is meant to police, which is worse than a failure
         * because it looks like a pass.
         */
        Assert.True(loops.Count >= 2,
            $"expected at least two per-stroke loops, found {loops.Count} - has the loop "
            + "been rewritten in a form this scan no longer recognises?");

        foreach (int start in loops)
        {
            int end = Math.Min(lines.Length, start + GuardWithinLines);
            bool guarded = Enumerable.Range(start, end - start)
                .Any(i => lines[i].Contains(Guard));

            Assert.True(guarded,
                $"the per-stroke loop at {Path.GetFileName(_operatorSource())}:{start + 1} "
                + $"has no {Guard} guard within {GuardWithinLines} lines. Without it this "
                + "fragment emits for strokes belonging to every other fragment too.");
        }
    }


    /**
     * Only a source that is flat by construction may claim to be, because that claim is
     * what removes the collision from under every street.
     */
    [Fact]
    public void OnlyTheFlatSourceClaimsToBeFlat()
    {
        var cd = StreetHarness.MakeCluster("isflat", 500f);

        Assert.True(new FlatStreetHeight(cd).IsFlat);
        Assert.False(new TerrainStreetHeight(cd).IsFlat);
        Assert.False(new FuncStreetHeight((x, z) => 12f).IsFlat,
            "a constant function is still not a promise of flatness");
    }


    /**
     * Relaxation only ever takes gradients out, so it cannot change the answer - and it
     * must not swallow it either, or a terrain city wrapped in a relaxer would report
     * itself flat and lose its road collision.
     */
    [Fact]
    public void RelaxationPassesFlatnessThrough()
    {
        var cd = StreetHarness.MakeCluster("isflat-relaxed", 500f);
        var policy = new GradePolicy();

        Assert.True(
            new RelaxedStreetHeight(cd, new FlatStreetHeight(cd), policy).IsFlat);
        Assert.False(
            new RelaxedStreetHeight(cd, new TerrainStreetHeight(cd), policy).IsFlat);
    }
}
