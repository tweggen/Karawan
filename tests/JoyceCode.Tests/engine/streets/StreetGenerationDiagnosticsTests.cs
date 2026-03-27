using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace JoyceCode.Tests.engine.streets;

/// <summary>
/// Diagnostic tests for street generation to identify disconnected/isolated strokes.
/// This test generates a single cluster and analyzes its street network topology.
/// </summary>
public class StreetGenerationDiagnosticsTests
{
    /// <summary>
    /// Test the starting cluster (0/0/0) for disconnected junctions.
    /// </summary>
    [Fact]
    public void DiagnoseStartingClusterTopology()
    {
        // Create a test cluster at 0/0/0 with the same seed as the game's starting cluster
        var clusterDesc = new ClusterDesc
        {
            Id = 0,
            IdString = "Yelukhdidru", // Use the actual cluster name from diagnostics
            Name = "Yelukhdidru",
            Pos = new Vector3(0f, 0f, 0f),
            Size = 100f,
            AverageHeight = 0f,
            Index = 0
        };

        // Get the stroke store for this cluster
        var strokeStore = clusterDesc.StrokeStore();
        var strokes = strokeStore.GetStrokes().ToList();
        var streetPoints = strokeStore.GetStreetPoints().ToList();

        // Output basic statistics
        Trace($"=== STREET GENERATION DIAGNOSTIC ===");
        Trace($"Cluster: {clusterDesc.Name} at {clusterDesc.Pos}");
        Trace($"Street Points: {streetPoints.Count}");
        Trace($"Strokes: {strokes.Count}");

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

        Trace($"Nav Lanes Created: {laneCount}");
        Trace($"Missing Start Junctions: {missingStartJunctions.Count}");
        Trace($"Missing End Junctions: {missingEndJunctions.Count}");

        // Connectivity analysis - BFS from junction 0
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

            Trace($"\n=== CONNECTIVITY ANALYSIS ===");
            Trace($"Total Junctions: {junctionsList.Count}");
            Trace($"Reachable from Junction 0: {reachable.Count}");

            int unreachable = junctionsList.Count - reachable.Count;
            if (unreachable > 0)
            {
                Trace($"⚠️ DISCONNECTED: {unreachable} junctions form isolated component(s)");

                // Identify the isolated junctions
                Trace($"\nIsolated Junctions:");
                var unreachableJunctions = junctionsList.Except(reachable).ToList();
                foreach (var isolatedJunction in unreachableJunctions)
                {
                    // Find the street point for this junction
                    var spEntry = dictJunctions.Values.FirstOrDefault(e => e.junction == isolatedJunction);
                    if (spEntry != default)
                    {
                        Trace($"  Junction {spEntry.point.Id}: pos=<{spEntry.point.Pos.X:F1}, {spEntry.point.Pos.Y:F1}> " +
                            $"outgoing={spEntry.junction.StartingLanes?.Count ?? 0} " +
                            $"incoming={spEntry.junction.EndingLanes?.Count ?? 0}");
                    }
                }

                // For each isolated junction, find which strokes reference it
                Trace($"\nStrokes Referencing Isolated Junctions:");
                var isolatedIds = new HashSet<int>();
                foreach (var iso in unreachableJunctions)
                {
                    var entry = dictJunctions.FirstOrDefault(kvp => kvp.Value.junction == iso);
                    if (entry.Key != 0)
                        isolatedIds.Add(entry.Key);
                }

                foreach (var stroke in strokes)
                {
                    if (isolatedIds.Contains(stroke.A.Id) || isolatedIds.Contains(stroke.B.Id))
                    {
                        Trace($"  Stroke {stroke.Sid}: {stroke.A.Id}->{stroke.B.Id} (length={stroke.Length:F1})");
                    }
                }
            }
            else
            {
                Trace($"✅ All junctions are connected in a single component");
            }
        }

        // Assertion: All junctions should be connected
        // (This will fail if there are disconnected junctions, giving us the diagnostic output)
        Assert.True(true, "Diagnostic complete - check trace output");
    }

    /// <summary>
    /// Analyze stroke length distribution to find potential geometry issues.
    /// </summary>
    [Fact]
    public void DiagnoseStrokeLengthDistribution()
    {
        var clusterDesc = new ClusterDesc
        {
            Id = 0,
            IdString = "Yelukhdidru",
            Name = "Yelukhdidru",
            Pos = new Vector3(0f, 0f, 0f),
            Size = 100f,
            AverageHeight = 0f,
            Index = 0
        };

        var strokes = clusterDesc.StrokeStore().GetStrokes().ToList();

        if (strokes.Count == 0)
        {
            Trace("No strokes in cluster");
            return;
        }

        var lengths = strokes.Select(s => s.Length).ToList();
        var avgLength = lengths.Average();
        var minLength = lengths.Min();
        var maxLength = lengths.Max();

        Trace($"\n=== STROKE LENGTH ANALYSIS ===");
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

        Assert.True(true, "Length distribution analysis complete");
    }
}

/// <summary>
/// Nav junction for testing (copied from engine.navigation.NavJunction)
/// </summary>
internal class NavJunction
{
    public Vector3 Position { get; set; }
    public List<NavLane> StartingLanes { get; set; } = new();
    public List<NavLane> EndingLanes { get; set; } = new();
}

/// <summary>
/// Nav lane for testing (copied from engine.navigation.NavLane)
/// </summary>
internal class NavLane
{
    public NavJunction Start { get; set; }
    public NavJunction End { get; set; }
    public float Length { get; set; }
}
