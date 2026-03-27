using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using DefaultEcs;
using engine;
using engine.world;
using static engine.Logger;


namespace builtin.modules.satnav;

/**
 * Generate car navigation mesh representing the street mesh.
 */
public class GenerateNavMapOperator : engine.world.IWorldOperator
{
    /**
     * Create the content for the individual clusters below the top
     * level cluster. We create NavLanes directly from the strokes, NavJunctions
     * directly from the StreetPoints.
     */
    private Task<NavClusterContent> _createClusterNavContentAsync(ClusterDesc clusterDesc, NavCluster ncTop)
    {
        Trace($"Loading cluster {clusterDesc.Name}");
        
        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };
        
        SortedDictionary<int, NavJunction> dictJunctions = new();
        foreach (var streetPoint in clusterDesc.StrokeStore().GetStreetPoints())
        {
            NavJunction nj = new()
            {
                Position = streetPoint.Pos3 + clusterDesc.Pos with { Y = clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight },
                StartingLanes = new(),
                EndingLanes = new()
            };
            dictJunctions[streetPoint.Id] = nj;
            ncc.Junctions.Add(nj);
        }

        int laneCount = 0;
        int skippedStrokes = 0;
        var strokes = clusterDesc.StrokeStore().GetStrokes();

        foreach (var stroke in strokes)
        {
            // Skip cross-cluster bridge strokes (not yet supported)
            // These are created by the bridging post-processor to connect orphaned clusters,
            // but inter-cluster navigation is not yet implemented.
            if (stroke.A.ClusterId != stroke.B.ClusterId)
            {
                Trace($"NavMap {clusterDesc.Name}: Skipping cross-cluster bridge stroke ({stroke.A.ClusterId} → {stroke.B.ClusterId})");
                skippedStrokes++;
                continue;
            }

            // Check if both endpoints exist in our junctions dictionary
            if (!dictJunctions.TryGetValue(stroke.A.Id, out var njA))
            {
                Trace($"NavMap {clusterDesc.Name}: Stroke missing start junction {stroke.A.Id}");
                skippedStrokes++;
                continue;
            }
            if (!dictJunctions.TryGetValue(stroke.B.Id, out var njB))
            {
                Trace($"NavMap {clusterDesc.Name}: Stroke missing end junction {stroke.B.Id}");
                skippedStrokes++;
                continue;
            }

            try
            {
                // Subdivide long lanes into shorter segments for proper pathfinding cursor snapping.
                // When two positions snap to endpoints of a long lane, they may snap to the same endpoint.
                // Intermediate junctions allow nearby positions to snap to different endpoints.
                const float MaxLaneLength = 50f; // Create intermediate junctions for lanes longer than this

                List<NavJunction> laneJunctions = new() { njA, njB };
                if (stroke.Length > MaxLaneLength)
                {
                    int segmentCount = (int)Single.Ceiling(stroke.Length / MaxLaneLength);
                    for (int i = 1; i < segmentCount; i++)
                    {
                        float t = (float)i / segmentCount;
                        Vector3 intermediatePos = Vector3.Lerp(njA.Position, njB.Position, t);

                        NavJunction njIntermediate = new()
                        {
                            Position = intermediatePos,
                            StartingLanes = new(),
                            EndingLanes = new()
                        };
                        ncc.Junctions.Add(njIntermediate);
                        laneJunctions.Insert(i, njIntermediate);
                    }
                }

                // Create lanes connecting consecutive junctions
                for (int i = 0; i < laneJunctions.Count - 1; i++)
                {
                    var njStart = laneJunctions[i];
                    var njEnd = laneJunctions[i + 1];
                    float segmentLength = Vector3.Distance(njStart.Position, njEnd.Position);

                    NavLane nlForth = new()
                    {
                        Start = njStart,
                        End = njEnd,
                        Length = segmentLength
                    };
                    njStart.StartingLanes.Add(nlForth);
                    njEnd.EndingLanes.Add(nlForth);
                    ncc.Lanes.Add(nlForth);

                    NavLane nlBack = new()
                    {
                        Start = njEnd,
                        End = njStart,
                        Length = segmentLength
                    };
                    njStart.EndingLanes.Add(nlBack);
                    njEnd.StartingLanes.Add(nlBack);
                    ncc.Lanes.Add(nlBack);
                    laneCount += 2;
                }
            }
            catch (Exception e)
            {
                Trace($"Exception adding navlane in {clusterDesc.Name}: {e}");
                skippedStrokes++;
            }
        }
        Trace($"NavMap cluster {clusterDesc.Name}: {dictJunctions.Count} junctions, {laneCount} lanes, {skippedStrokes}/{strokes.Count} strokes skipped");

        // Connectivity check: how many junctions are reachable from junction 0?
        if (ncc.Junctions.Count > 0)
        {
            var reachable = new HashSet<NavJunction>();
            var queue = new Queue<NavJunction>();
            queue.Enqueue(ncc.Junctions[0]);
            reachable.Add(ncc.Junctions[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var lane in current.StartingLanes ?? new())
                {
                    if (reachable.Add(lane.End))
                        queue.Enqueue(lane.End);
                }
            }

            Trace($"NavMap cluster {clusterDesc.Name}: {reachable.Count}/{ncc.Junctions.Count} junctions reachable from junction 0");
            if (reachable.Count < ncc.Junctions.Count)
            {
                Trace($"  ⚠️ DISCONNECTED: {ncc.Junctions.Count - reachable.Count} junctions unreachable (isolated components)");
            }
        }

        return Task.FromResult(ncc);
    }

    
    /**
     * Create the top level cluster content by creating sub-NavClusters
     * for each of our clusters.
     */
    private Task<NavClusterContent> _createTopClusterContentAsync(NavCluster ncTop)
    {
        Trace($"Loading top level cluster");

        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };
        
        var clusterList = I.Get<ClusterList>().GetClusterList();

        foreach (var clusterDesc in clusterList)
        {
            NavCluster nc = new()
            {
                Id = clusterDesc.IdString,
                AABB = clusterDesc.AABB,
                ParentCluster = ncTop,
                CreateClusterContentAsync = (NavCluster nc) => _createClusterNavContentAsync(clusterDesc, nc),
                Content = null
            };

            ncc.Clusters.Add(nc);
        }

        return Task.FromResult(ncc);
    }
    
    
    public string WorldOperatorGetPath()
    {
        return "builtin.modules.satnav/GenerateNavMapOperator";
    }


    public System.Func<Task> WorldOperatorApply() => new (async () =>
    {
        NavCluster ncTop = new()
        {
            Id = "Top",
            CreateClusterContentAsync = _createTopClusterContentAsync,
            AABB = MetaGen.AABB
        };
        
        I.Get<NavMap>().TopCluster = ncTop;
        

        Trace("GenerateNavMapOperator: Done.");
    });
    

    public GenerateNavMapOperator()
    {
    }
}