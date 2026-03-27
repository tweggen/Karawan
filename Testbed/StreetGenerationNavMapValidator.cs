using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav.desc;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace Testbed;

/// <summary>
/// Validates NavMap connectivity independently from street generation.
/// Reconstructs the NavJunction/NavLane graph and checks if all lanes are reachable.
/// </summary>
public class StreetGenerationNavMapValidator
{
    /// <summary>
    /// Validate NavMap connectivity for a cluster.
    /// </summary>
    public static void ValidateNavMapConnectivity(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null)
        {
            Trace("Error: ClusterDesc is null");
            return;
        }

        var strokeStore = clusterDesc.StrokeStore();
        var strokes = strokeStore.GetStrokes().ToList();
        var streetPoints = strokeStore.GetStreetPoints().ToList();

        Trace($"\n{'='} NAVMAP CONNECTIVITY VALIDATION ===============");
        Trace($"Cluster: {clusterDesc.Name}");

        if (streetPoints.Count == 0 || strokes.Count == 0)
        {
            Trace("No street network to validate");
            return;
        }

        // Reconstruct NavJunction/NavLane graph (same as GenerateNavMapOperator)
        var dictJunctions = new Dictionary<int, NavJunction>();
        var lanesList = new List<NavLane>();

        // Create junctions from street points
        foreach (var streetPoint in streetPoints)
        {
            var nj = new NavJunction
            {
                Position = new Vector3(streetPoint.Pos.X, 0f, streetPoint.Pos.Y),
                StartingLanes = new(),
                EndingLanes = new()
            };
            dictJunctions[streetPoint.Id] = nj;
        }

        // Create lanes from strokes
        int skippedStrokes = 0;
        foreach (var stroke in strokes)
        {
            if (!dictJunctions.TryGetValue(stroke.A.Id, out var njA))
            {
                skippedStrokes++;
                continue;
            }
            if (!dictJunctions.TryGetValue(stroke.B.Id, out var njB))
            {
                skippedStrokes++;
                continue;
            }

            // Forward lane
            var nlForth = new NavLane
            {
                Start = njA,
                End = njB,
                Length = stroke.Length
            };
            njA.StartingLanes.Add(nlForth);
            njB.EndingLanes.Add(nlForth);
            lanesList.Add(nlForth);

            // Backward lane
            var nlBack = new NavLane
            {
                Start = njB,
                End = njA,
                Length = stroke.Length
            };
            njB.StartingLanes.Add(nlBack);
            njA.EndingLanes.Add(nlBack);
            lanesList.Add(nlBack);
        }

        Trace($"Total NavJunctions: {dictJunctions.Count}");
        Trace($"Total NavLanes: {lanesList.Count}");
        Trace($"Skipped strokes (missing endpoints): {skippedStrokes}");

        // Analyze connectivity: BFS from each lane
        var reachableLanesFromStart = new HashSet<NavLane>();
        if (lanesList.Count > 0)
        {
            var startLane = lanesList[0];
            BfsReachableLanes(startLane, reachableLanesFromStart);
        }

        int unreachableLaneCount = lanesList.Count - reachableLanesFromStart.Count;

        Trace($"\nConnectivity Analysis (from first lane):");
        Trace($"  Reachable lanes: {reachableLanesFromStart.Count}/{lanesList.Count}");
        Trace($"  Unreachable lanes: {unreachableLaneCount}");

        if (unreachableLaneCount > 0)
        {
            Trace($"  ⚠️ WARNING: {unreachableLaneCount} lanes are unreachable!");

            // Analyze unreachable lanes
            var unreachableLanes = lanesList.Except(reachableLanesFromStart).ToList();
            var unreachableJunctions = new HashSet<NavJunction>();

            foreach (var lane in unreachableLanes)
            {
                unreachableJunctions.Add(lane.Start);
                unreachableJunctions.Add(lane.End);
            }

            Trace($"  Unreachable junctions: {unreachableJunctions.Count}");

            // Group unreachable lanes by their start junction to identify isolated clusters
            var lanesByStartJunction = unreachableLanes.GroupBy(l => l.Start).ToList();
            Trace($"  Unreachable start junctions: {lanesByStartJunction.Count}");

            // Sample some unreachable lanes
            foreach (var lane in unreachableLanes.Take(5))
            {
                Trace($"    Unreachable lane: {lane.Start.Position} → {lane.End.Position} ({lane.Length:F1}m)");
            }
            if (unreachableLanes.Count > 5)
            {
                Trace($"    ... and {unreachableLanes.Count - 5} more unreachable lanes");
            }
        }
        else
        {
            Trace($"  ✅ All lanes are reachable - NavMap is fully connected");
        }

        // Analyze junction-level connectivity
        var reachableJunctions = new HashSet<NavJunction>();
        if (lanesList.Count > 0)
        {
            BfsReachableJunctions(lanesList[0].Start, reachableJunctions);
        }

        int unreachableJunctionCount = dictJunctions.Count - reachableJunctions.Count;
        Trace($"\nJunction Connectivity (from first junction):");
        Trace($"  Reachable junctions: {reachableJunctions.Count}/{dictJunctions.Count}");
        Trace($"  Unreachable junctions: {unreachableJunctionCount}");

        if (unreachableJunctionCount > 0)
        {
            Trace($"  ⚠️ WARNING: {unreachableJunctionCount} junctions are unreachable!");
        }
        else
        {
            Trace($"  ✅ All junctions are reachable - full junction connectivity");
        }
    }

    /// <summary>
    /// BFS to find all reachable lanes from a starting lane.
    /// </summary>
    private static void BfsReachableLanes(NavLane startLane, HashSet<NavLane> reachable)
    {
        var queue = new Queue<NavLane>();
        queue.Enqueue(startLane);
        reachable.Add(startLane);

        while (queue.Count > 0)
        {
            var currentLane = queue.Dequeue();
            var endJunction = currentLane.End;

            // Find all lanes starting from the end junction of current lane
            foreach (var nextLane in endJunction.StartingLanes)
            {
                if (!reachable.Contains(nextLane))
                {
                    reachable.Add(nextLane);
                    queue.Enqueue(nextLane);
                }
            }
        }
    }

    /// <summary>
    /// BFS to find all reachable junctions from a starting junction.
    /// </summary>
    private static void BfsReachableJunctions(NavJunction startJunction, HashSet<NavJunction> reachable)
    {
        var queue = new Queue<NavJunction>();
        queue.Enqueue(startJunction);
        reachable.Add(startJunction);

        while (queue.Count > 0)
        {
            var currentJunction = queue.Dequeue();

            // Find all junctions reachable via lanes from current junction
            foreach (var lane in currentJunction.StartingLanes)
            {
                if (!reachable.Contains(lane.End))
                {
                    reachable.Add(lane.End);
                    queue.Enqueue(lane.End);
                }
            }
        }
    }
}
