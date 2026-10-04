using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using engine.world;

namespace JoyceCode.Tests.engine.streets;


/**
 * Drives engine.streets.Generator directly.
 *
 * Deliberately does NOT go through ClusterDesc.StrokeStore(): that path needs
 * ClusterStorage registered in the I container and consults the cluster cache, so it
 * may not generate at all. Generator and Stroke.CreateByAngleFrom only ever read
 * ClusterDesc.Id and ClusterDesc.Size, so a bare ClusterDesc is enough here.
 *
 * Seeding and bounds go through engine.streets.StreetSeeds — the same code the game
 * uses — so this harness cannot drift into testing a lookalike of the real thing.
 */
internal static class StreetHarness
{
    internal static ClusterDesc MakeCluster(string idString, float size)
    {
        return new ClusterDesc
        {
            Id = 0,
            IdString = idString,
            Name = idString,
            Pos = Vector3.Zero,
            Size = size,
            AverageHeight = 0f,
            Index = 0
        };
    }


    /**
     * Generate the street network for a cluster of the given seed string and size.
     * Mirrors ClusterDesc._generateStrokes exactly.
     */
    internal static StrokeStore Generate(string idString, float size)
        => Generate(idString, size, null);


    /**
     * @param ruleTable
     *     Ruleset to grow with, or null for the built-in defaults.
     */
    internal static StrokeStore Generate(string idString, float size, ExpansionRuleTable ruleTable)
        => Generate(idString, size, ruleTable, gradeSeparation: false, onCandidatePopped: null);


    /**
     * The same cluster with grade separation enabled: the candidate queue drains
     * heaviest first (WP-B2) and the finished network then has structures placed on it
     * (WP-B3b).
     *
     * The ground under it is FLAT here, which is not a shortcut - it is the flat game
     * with the flag on, which §5 says is a world the setting is allowed to describe. It
     * is also the strictest gate for placement, because on level ground every corridor's
     * deck grade is zero and nothing is refused for it, so the geometry rules stand
     * alone. What the shipped terrain does to the same corridors is measured separately,
     * over ShippedTerrain, in StructurePlacementTests.
     */
    internal static StrokeStore GenerateHeavyFirst(string idString, float size)
        => Generate(idString, size, null, gradeSeparation: true, onCandidatePopped: null);


    /**
     * ...and the generator that built it, for a caller that wants its placement report.
     */
    internal static (StrokeStore Store, Generator Generator) GenerateHeavyFirstReporting(
        string idString, float size, Func<StreetPoint, float> groundHeightOf)
    {
        var clusterDesc = MakeCluster(idString, size);
        var store = _generate(clusterDesc, idString, null, true, null, groundHeightOf,
            out var generator);
        return (store, generator);
    }


    /**
     * @param gradeSeparation
     *     Whether this run may build structures, and - WP-B2 - orders its queue by
     *     weight. Exactly what ClusterDesc._generateStrokes passes in from the
     *     joyce.EnableGradeSeparation setting.
     * @param onCandidatePopped
     *     Observer called with each candidate as it leaves the generator's queue and
     *     everything still waiting behind it. The heavy-first ordering is a property of
     *     that order and of nothing else visible from outside.
     */
    internal static StrokeStore Generate(
        string idString, float size, ExpansionRuleTable ruleTable, bool gradeSeparation,
        Action<Stroke, IReadOnlyList<Stroke>> onCandidatePopped)
    {
        var clusterDesc = MakeCluster(idString, size);
        return _generate(
            clusterDesc, idString, ruleTable, gradeSeparation, onCandidatePopped,
            groundHeightOf: null, out _);
    }


    /**
     * @param groundHeightOf
     *     Ground under a junction for the placement pass, or null to mirror what
     *     ClusterDesc._generateStrokes does with a FLAT cluster - which is what
     *     MakeCluster builds, AverageHeight and all.
     */
    private static StrokeStore _generate(
        ClusterDesc clusterDesc, string idString, ExpansionRuleTable ruleTable,
        bool gradeSeparation, Action<Stroke, IReadOnlyList<Stroke>> onCandidatePopped,
        Func<StreetPoint, float> groundHeightOf, out Generator generator)
    {
        var strokeStore = new StrokeStore(clusterDesc.Size);

        var streetGenerator = new Generator();
        streetGenerator.SetAnnotation($"Cluster {clusterDesc.Name}");
        streetGenerator.Reset("streets-" + idString, strokeStore, clusterDesc);
        streetGenerator.RuleTable = ruleTable;
        streetGenerator.EnableGradeSeparation = gradeSeparation;
        streetGenerator.OnCandidatePopped = onCandidatePopped;

        /*
         * Exactly ClusterDesc._generateStrokes' own line, so that the harness cannot
         * drift into testing a lookalike of the height the game would supply.
         */
        streetGenerator.GroundHeightOf = groundHeightOf
            ?? StreetHeightSources.UnrelaxedOf(new FlatStreetHeight(clusterDesc)).GroundHeightAt;

        StreetSeeds.ApplyBounds(streetGenerator, clusterDesc);
        StreetSeeds.AddTo(streetGenerator, clusterDesc, clusterDesc.Rnd);
        streetGenerator.Generate();

        generator = streetGenerator;
        return strokeStore;
    }


    /**
     * Trace the city blocks of an already generated network, exactly as
     * ClusterDesc._findQuarters does.
     *
     * QuarterGenerator needs nothing from the container, so a bare cluster and a stroke
     * store are enough - which is what lets a block's floor be checked against real
     * generated cities rather than a hand-built ring.
     */
    internal static QuarterStore GenerateQuarters(
        ClusterDesc clusterDesc, StrokeStore strokeStore, string idString)
    {
        var quarterStore = new QuarterStore(clusterDesc);
        var quarterGenerator = new QuarterGenerator();
        quarterGenerator.Reset("quarters-" + idString, clusterDesc, quarterStore, strokeStore);
        quarterGenerator.Generate();

        return quarterStore;
    }


    /**
     * Number of connected components in the stroke graph. A healthy cluster is a
     * single component; ConnectComponentsPass (WP-2c) is what keeps it that way.
     */
    internal static int CountComponents(StrokeStore store)
    {
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var sp in store.GetStreetPoints())
        {
            adjacency[sp.Id] = new List<int>();
        }

        foreach (var stroke in store.GetStrokes())
        {
            if (!adjacency.ContainsKey(stroke.A.Id) || !adjacency.ContainsKey(stroke.B.Id))
            {
                continue;
            }
            adjacency[stroke.A.Id].Add(stroke.B.Id);
            adjacency[stroke.B.Id].Add(stroke.A.Id);
        }

        var visited = new HashSet<int>();
        int components = 0;

        foreach (var startId in adjacency.Keys)
        {
            if (!visited.Add(startId)) continue;

            ++components;
            var queue = new Queue<int>();
            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbour in adjacency[current])
                {
                    if (visited.Add(neighbour)) queue.Enqueue(neighbour);
                }
            }
        }

        return components;
    }
}
