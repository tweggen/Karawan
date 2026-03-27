using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace Testbed;

/// <summary>
/// Diagnostic tool for analyzing street network topology and identifying disconnected junctions.
/// </summary>
public class StreetGenerationDiagnostics
{
    /// <summary>
    /// Simple NavJunction representation for analysis.
    /// </summary>
    private class NavJunction
    {
        public Vector3 Position { get; set; }
        public List<NavLane> StartingLanes { get; set; } = new();
        public List<NavLane> EndingLanes { get; set; } = new();
    }

    /// <summary>
    /// Simple NavLane representation for analysis.
    /// </summary>
    private class NavLane
    {
        public NavJunction Start { get; set; }
        public NavJunction End { get; set; }
        public float Length { get; set; }
    }

    /// <summary>
    /// Run complete street generation diagnostics on a cluster.
    /// </summary>
    public static void DiagnoseCluster(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null)
        {
            Trace("Error: ClusterDesc is null");
            return;
        }

        // Get the stroke store for this cluster
        var strokeStore = clusterDesc.StrokeStore();
        var strokes = strokeStore.GetStrokes().ToList();
        var streetPoints = strokeStore.GetStreetPoints().ToList();

        // Output basic statistics
        Trace($"\n{'='} STREET GENERATION DIAGNOSTIC =================");
        Trace($"Cluster: {clusterDesc.Name} (ID={clusterDesc.Id})");
        Trace($"Position: {clusterDesc.Pos}");
        Trace($"IdString: {clusterDesc.IdString}");
        Trace($"Street Points (junctions): {streetPoints.Count}");
        Trace($"Strokes: {strokes.Count}");

        if (streetPoints.Count == 0 || strokes.Count == 0)
        {
            Trace("No street network to analyze");
            return;
        }

        // Convert strokes to junctions and lanes (same logic as GenerateNavMapOperator)
        var dictJunctions = new Dictionary<int, (StreetPoint point, NavJunction junction)>();
        var junctionsList = new List<NavJunction>();

        // Create junctions from street points
        foreach (var streetPoint in streetPoints)
        {
            var nj = new NavJunction
            {
                Position = new Vector3(streetPoint.Pos.X, 0f, streetPoint.Pos.Y),
                StartingLanes = new(),
                EndingLanes = new()
            };
            dictJunctions[streetPoint.Id] = (streetPoint, nj);
            junctionsList.Add(nj);
        }

        // Create lanes from strokes and track which endpoints exist
        int laneCount = 0;
        var missingStartJunctions = new HashSet<int>();
        var missingEndJunctions = new HashSet<int>();

        foreach (var stroke in strokes)
        {
            // Check if both endpoints exist
            if (!dictJunctions.TryGetValue(stroke.A.Id, out var jA))
            {
                missingStartJunctions.Add(stroke.A.Id);
                continue;
            }
            if (!dictJunctions.TryGetValue(stroke.B.Id, out var jB))
            {
                missingEndJunctions.Add(stroke.B.Id);
                continue;
            }

            // Create bidirectional lanes
            var nlForth = new NavLane
            {
                Start = jA.junction,
                End = jB.junction,
                Length = stroke.Length
            };
            jA.junction.StartingLanes.Add(nlForth);
            jB.junction.EndingLanes.Add(nlForth);

            var nlBack = new NavLane
            {
                Start = jB.junction,
                End = jA.junction,
                Length = stroke.Length
            };
            jB.junction.EndingLanes.Add(nlBack);
            jA.junction.StartingLanes.Add(nlBack);

            laneCount += 2;
        }

        Trace($"Nav Lanes (bidirectional): {laneCount}");
        if (missingStartJunctions.Count > 0)
            Trace($"⚠️ Missing start junctions: {missingStartJunctions.Count}");
        if (missingEndJunctions.Count > 0)
            Trace($"⚠️ Missing end junctions: {missingEndJunctions.Count}");

        // Connectivity analysis - BFS from junction 0
        Trace($"\n{'='} CONNECTIVITY ANALYSIS =======================");
        if (junctionsList.Count > 0)
        {
            var reachable = new HashSet<NavJunction>();
            var queue = new Queue<NavJunction>();
            queue.Enqueue(junctionsList[0]);
            reachable.Add(junctionsList[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.StartingLanes != null)
                {
                    foreach (var lane in current.StartingLanes)
                    {
                        if (reachable.Add(lane.End))
                            queue.Enqueue(lane.End);
                    }
                }
            }

            Trace($"Total Junctions: {junctionsList.Count}");
            Trace($"Reachable from Junction 0: {reachable.Count}");

            int unreachable = junctionsList.Count - reachable.Count;
            if (unreachable > 0)
            {
                Trace($"⚠️ DISCONNECTED: {unreachable} junctions form isolated component(s)");

                // Identify the isolated junctions
                Trace($"\nIsolated Junctions:");
                var unreachableJunctions = junctionsList.Except(reachable).ToList();
                var isolatedJunctionIds = new HashSet<int>();

                foreach (var isolatedJunction in unreachableJunctions)
                {
                    // Find the street point for this junction
                    var entry = dictJunctions.FirstOrDefault(kvp => kvp.Value.junction == isolatedJunction);
                    if (entry.Key != 0 || entry.Value.junction != null)
                    {
                        var spEntry = entry.Value;
                        isolatedJunctionIds.Add(spEntry.point.Id);
                        Trace($"  Junction {spEntry.point.Id}: pos=<{spEntry.point.Pos.X:F1}, {spEntry.point.Pos.Y:F1}> " +
                            $"(out={spEntry.junction.StartingLanes?.Count ?? 0}, in={spEntry.junction.EndingLanes?.Count ?? 0})");
                    }
                }

                // For each isolated junction, find which strokes reference it
                Trace($"\nStrokes Connecting to Isolated Junctions:");
                int strokeCount = 0;
                foreach (var stroke in strokes)
                {
                    if (isolatedJunctionIds.Contains(stroke.A.Id) || isolatedJunctionIds.Contains(stroke.B.Id))
                    {
                        Trace($"  Stroke {stroke.Sid}: {stroke.A.Id} → {stroke.B.Id} (len={stroke.Length:F1}m, weight={stroke.Weight:F2})");
                        strokeCount++;
                    }
                }
                if (strokeCount == 0)
                    Trace($"  (No strokes reference isolated junctions - junction references may be missing)");
            }
            else
            {
                Trace($"✅ All {junctionsList.Count} junctions are connected in a single component");
            }
        }

        // Stroke length analysis
        Trace($"\n{'='} STROKE LENGTH ANALYSIS =======================");
        AnalyzeStrokeLengths(strokes);
    }

    /// <summary>
    /// Analyze stroke length distribution.
    /// </summary>
    private static void AnalyzeStrokeLengths(List<Stroke> strokes)
    {
        if (strokes.Count == 0)
        {
            Trace("No strokes to analyze");
            return;
        }

        var lengths = strokes.Select(s => s.Length).ToList();
        var avgLength = lengths.Average();
        var minLength = lengths.Min();
        var maxLength = lengths.Max();

        Trace($"Total Strokes: {strokes.Count}");
        Trace($"Min Length: {minLength:F1}m");
        Trace($"Max Length: {maxLength:F1}m");
        Trace($"Avg Length: {avgLength:F1}m");

        // Group by length ranges
        var rangeGroups = new Dictionary<string, int>
        {
            { "0-5m", 0 },
            { "5-10m", 0 },
            { "10-20m", 0 },
            { "20-30m", 0 },
            { ">30m", 0 }
        };

        foreach (var stroke in strokes)
        {
            if (stroke.Length < 5) rangeGroups["0-5m"]++;
            else if (stroke.Length < 10) rangeGroups["5-10m"]++;
            else if (stroke.Length < 20) rangeGroups["10-20m"]++;
            else if (stroke.Length < 30) rangeGroups["20-30m"]++;
            else rangeGroups[">30m"]++;
        }

        Trace($"\nStroke Length Distribution:");
        foreach (var kvp in rangeGroups)
        {
            Trace($"  {kvp.Key}: {kvp.Value} strokes");
        }
    }

    /// <summary>
    /// Run diagnostics on all clusters in the cluster list.
    /// </summary>
    public static void DiagnoseAllClusters()
    {
        var clusterList = engine.I.Get<ClusterList>();
        if (clusterList == null)
        {
            Trace("Error: ClusterList not available");
            return;
        }

        var clusters = clusterList.GetClusterList();
        Trace($"\n{'#'} DIAGNOSING {clusters.Count} CLUSTERS #####");

        foreach (var cluster in clusters)
        {
            DiagnoseCluster(cluster);
            StreetGenerationAnalysis.AnalyzeOrphanedJunctions(cluster);
            StreetGenerationAnalysis.AnalyzeBoundaryPoints(cluster);
            Trace("");  // Blank line between clusters
        }

        Trace($"{'#'} DIAGNOSTICS COMPLETE #####");
    }
}
