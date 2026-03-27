using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace Testbed;

/// <summary>
/// Deeper analysis of orphaned junctions - traces back to find the root cause.
/// The street generation algorithm:
/// 1. Starts with seed strokes in 4 corners
/// 2. Each stroke can generate forward, left, right, and random branches
/// 3. When a stroke fails validation, it's discarded but its endpoints may persist
/// 4. This creates orphaned StreetPoints with no connecting Strokes
/// </summary>
public class StreetGenerationAnalysis
{
    /// <summary>
    /// Analyze which StreetPoints have no strokes connecting to them.
    /// </summary>
    public static void AnalyzeOrphanedJunctions(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null)
            return;

        var strokeStore = clusterDesc.StrokeStore();
        var streetPoints = strokeStore.GetStreetPoints().ToList();
        var strokes = strokeStore.GetStrokes().ToList();

        Trace($"\n{'='} ORPHANED JUNCTION ANALYSIS ===========");
        Trace($"Cluster: {clusterDesc.Name}");
        Trace($"Total StreetPoints: {streetPoints.Count}");
        Trace($"Total Strokes: {strokes.Count}");

        // Build a map of which StreetPoints are referenced by strokes
        var pointsReferencedByStrokes = new HashSet<int>();
        var pointOutgoingLanes = new Dictionary<int, int>();
        var pointIncomingLanes = new Dictionary<int, int>();

        foreach (var sp in streetPoints)
        {
            pointOutgoingLanes[sp.Id] = 0;
            pointIncomingLanes[sp.Id] = 0;
        }

        foreach (var stroke in strokes)
        {
            pointsReferencedByStrokes.Add(stroke.A.Id);
            pointsReferencedByStrokes.Add(stroke.B.Id);

            pointOutgoingLanes[stroke.A.Id]++;
            pointIncomingLanes[stroke.B.Id]++;
        }

        // Find orphaned points: exist in StreetPoints but no strokes reference them
        var orphanedPoints = streetPoints.Where(sp => !pointsReferencedByStrokes.Contains(sp.Id)).ToList();

        if (orphanedPoints.Count == 0)
        {
            Trace($"✅ No orphaned points - all StreetPoints have connecting strokes");
            return;
        }

        Trace($"⚠️  ORPHANED STREETPOINTS: {orphanedPoints.Count}");
        Trace($"\nThese exist but are NOT referenced by any Stroke:");

        foreach (var orphan in orphanedPoints.OrderBy(p => p.Id))
        {
            Trace($"  StreetPoint {orphan.Id}: pos=<{orphan.Pos.X:F1}, {orphan.Pos.Y:F1}> creator='{orphan.Creator}'");
        }

        // Find dead-end points: only have incoming OR outgoing, not both
        var deadEnds = new List<int>();
        foreach (var sp in streetPoints)
        {
            int outgoing = pointOutgoingLanes.GetValueOrDefault(sp.Id, 0);
            int incoming = pointIncomingLanes.GetValueOrDefault(sp.Id, 0);

            if ((outgoing == 0 && incoming > 0) || (outgoing > 0 && incoming == 0))
            {
                deadEnds.Add(sp.Id);
            }
        }

        if (deadEnds.Count > 0)
        {
            Trace($"\n⚠️  DEAD-END POINTS: {deadEnds.Count}");
            Trace($"(These have outgoing OR incoming, but not both - represent street dead ends):");

            var endPoints = streetPoints.Where(sp => deadEnds.Contains(sp.Id)).ToList();
            foreach (var endpoint in endPoints.OrderBy(p => p.Id).Take(20))
            {
                int out_count = pointOutgoingLanes.GetValueOrDefault(endpoint.Id, 0);
                int in_count = pointIncomingLanes.GetValueOrDefault(endpoint.Id, 0);
                Trace($"  StreetPoint {endpoint.Id}: pos=<{endpoint.Pos.X:F1}, {endpoint.Pos.Y:F1}> " +
                    $"out={out_count} in={in_count} creator='{endpoint.Creator}'");
            }
            if (endPoints.Count > 20)
                Trace($"  ... and {endPoints.Count - 20} more");
        }

        // Analyze isolated components by checking connectivity
        Trace($"\n{'='} CONNECTIVITY BREAKDOWN ==================");

        var componentMap = new Dictionary<int, int>();
        int componentId = 0;

        foreach (var sp in streetPoints)
        {
            if (componentMap.ContainsKey(sp.Id))
                continue;

            // BFS from this point to find all connected points
            var queue = new Queue<int>();
            var component = new HashSet<int>();
            queue.Enqueue(sp.Id);
            component.Add(sp.Id);
            componentId++;

            while (queue.Count > 0)
            {
                int currentId = queue.Dequeue();

                // Find all strokes connected to this point
                foreach (var stroke in strokes)
                {
                    int nextId = -1;
                    if (stroke.A.Id == currentId)
                        nextId = stroke.B.Id;
                    else if (stroke.B.Id == currentId)
                        nextId = stroke.A.Id;

                    if (nextId >= 0 && component.Add(nextId))
                    {
                        queue.Enqueue(nextId);
                    }
                }
            }

            // Assign all points in this component
            foreach (var ptId in component)
            {
                componentMap[ptId] = componentId;
            }
        }

        var componentSizes = new Dictionary<int, int>();
        foreach (var compId in componentMap.Values)
        {
            if (!componentSizes.ContainsKey(compId))
                componentSizes[compId] = 0;
            componentSizes[compId]++;
        }

        Trace($"Total Connected Components: {componentSizes.Count}");

        var sortedComponents = componentSizes.OrderByDescending(kvp => kvp.Value).ToList();
        for (int i = 0; i < sortedComponents.Count; i++)
        {
            if (sortedComponents[i].Value >= 5) // Only show sizeable components
            {
                Trace($"  Component {i+1}: {sortedComponents[i].Value} points");
            }
        }

        if (componentSizes.Count > 1)
        {
            Trace($"\n⚠️  DISCONNECTED COMPONENTS DETECTED");
            var isolatedComponents = componentSizes.Where(kvp => kvp.Value < 5).ToList();
            if (isolatedComponents.Count > 0)
            {
                Trace($"Small isolated components: {isolatedComponents.Count}");
                foreach (var (cid, size) in isolatedComponents)
                {
                    var componentPoints = streetPoints.Where(sp => componentMap.ContainsKey(sp.Id) && componentMap[sp.Id] == cid).ToList();
                    Trace($"  Component size {size}:");
                    foreach (var pt in componentPoints.OrderBy(p => p.Id))
                    {
                        int out_count = pointOutgoingLanes.GetValueOrDefault(pt.Id, 0);
                        int in_count = pointIncomingLanes.GetValueOrDefault(pt.Id, 0);
                        Trace($"    StreetPoint {pt.Id}: <{pt.Pos.X:F1}, {pt.Pos.Y:F1}> out={out_count} in={in_count}");
                    }
                }
            }
        }

        // Analysis of why junctions are orphaned
        Trace($"\n{'='} ROOT CAUSE ANALYSIS =====================");
        Trace($"The orphaned junctions likely result from:");
        Trace($"1. Seed strokes created at cluster corners (0, 0), (0, size), (size, 0), (size, size)");
        Trace($"2. If a seed stroke fails validation, it's discarded but might leave orphaned endpoints");
        Trace($"3. Alternative: Some StreetPoints are created by QuarterGenerator separately from Strokes");
        Trace($"4. Result: StreetPoints exist without any Stroke connecting them");
    }

    /// <summary>
    /// Check which StreetPoints are at cluster boundaries (likely seed points).
    /// </summary>
    public static void AnalyzeBoundaryPoints(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null)
            return;

        var streetPoints = clusterDesc.StrokeStore().GetStreetPoints().ToList();
        float halfSize = clusterDesc.Size / 2f;
        float boundaryMargin = 20f; // Points within this distance of boundary

        var boundaryPoints = streetPoints.Where(sp =>
            Math.Abs(Math.Abs(sp.Pos.X) - halfSize) < boundaryMargin ||
            Math.Abs(Math.Abs(sp.Pos.Y) - halfSize) < boundaryMargin
        ).ToList();

        Trace($"\n{'='} BOUNDARY POINT ANALYSIS ==================");
        Trace($"Cluster size: {clusterDesc.Size}");
        Trace($"Boundary margin: ±{boundaryMargin}m from ±{halfSize}");
        Trace($"Points at cluster boundaries: {boundaryPoints.Count}");

        if (boundaryPoints.Count > 0)
        {
            Trace($"\nSample boundary points (likely seed or edge points):");
            foreach (var pt in boundaryPoints.OrderBy(p => p.Id).Take(10))
            {
                string cornerName = DetermineCorner(pt.Pos, halfSize);
                Trace($"  StreetPoint {pt.Id}: pos=<{pt.Pos.X:F1}, {pt.Pos.Y:F1}> {cornerName} creator='{pt.Creator}'");
            }
            if (boundaryPoints.Count > 10)
                Trace($"  ... and {boundaryPoints.Count - 10} more");
        }
    }

    private static string DetermineCorner(Vector2 pos, float halfSize)
    {
        if (pos.X > 0 && pos.Y > 0) return "(NE corner)";
        if (pos.X < 0 && pos.Y > 0) return "(NW corner)";
        if (pos.X < 0 && pos.Y < 0) return "(SW corner)";
        if (pos.X > 0 && pos.Y < 0) return "(SE corner)";
        return "(boundary)";
    }
}
