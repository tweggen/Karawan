using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using DefaultEcs;
using engine;
using engine.navigation;
using engine.world;
using static engine.Logger;


namespace builtin.modules.satnav;

/**
 * Generate combined car + pedestrian navigation mesh.
 * Car lanes follow street centerlines (from Strokes).
 * Pedestrian lanes follow sidewalks (from Quarter boundaries).
 * Crossing lanes connect sidewalks across streets at intersections.
 */
public class GenerateNavMapOperator : engine.world.IWorldOperator
{
    private const float MaxLaneLength = 50f;


    /**
     * Create bidirectional lanes between two junctions, subdividing if the
     * distance exceeds MaxLaneLength. Returns the number of lanes created.
     */
    private int _createBidirectionalLanes(
        NavJunction njA, NavJunction njB,
        TransportationType allowedType,
        NavClusterContent ncc)
    {
        float totalLength = Vector3.Distance(njA.Position, njB.Position);
        if (totalLength < 0.01f) return 0;

        List<NavJunction> junctions = new() { njA, njB };
        if (totalLength > MaxLaneLength)
        {
            int segmentCount = (int)Single.Ceiling(totalLength / MaxLaneLength);
            for (int i = 1; i < segmentCount; i++)
            {
                float t = (float)i / segmentCount;
                NavJunction njIntermediate = new()
                {
                    Position = Vector3.Lerp(njA.Position, njB.Position, t),
                    StartingLanes = new(),
                    EndingLanes = new()
                };
                ncc.Junctions.Add(njIntermediate);
                junctions.Insert(i, njIntermediate);
            }
        }

        int count = 0;
        for (int i = 0; i < junctions.Count - 1; i++)
        {
            var njStart = junctions[i];
            var njEnd = junctions[i + 1];
            float segmentLength = Vector3.Distance(njStart.Position, njEnd.Position);

            NavLane nlForth = new()
            {
                Start = njStart,
                End = njEnd,
                Length = segmentLength,
                AllowedTypes = new TransportationTypeFlags(allowedType)
            };
            njStart.StartingLanes.Add(nlForth);
            njEnd.EndingLanes.Add(nlForth);
            ncc.Lanes.Add(nlForth);

            NavLane nlBack = new()
            {
                Start = njEnd,
                End = njStart,
                Length = segmentLength,
                AllowedTypes = new TransportationTypeFlags(allowedType)
            };
            njStart.EndingLanes.Add(nlBack);
            njEnd.StartingLanes.Add(nlBack);
            ncc.Lanes.Add(nlBack);
            count += 2;
        }

        return count;
    }


    /**
     * Create the content for the individual clusters below the top
     * level cluster.
     *
     * Car lanes are created from Strokes (street centerlines).
     * Pedestrian lanes are created from Quarter boundaries (sidewalks).
     * Crossing lanes connect sidewalk junctions at each intersection.
     */
    private Task<NavClusterContent> _createClusterNavContentAsync(ClusterDesc clusterDesc, NavCluster ncTop)
    {
        Trace($"Loading cluster {clusterDesc.Name}");

        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };

        float navY = clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight;

        /*
         * === Car Lanes (from Strokes) ===
         */
        SortedDictionary<int, NavJunction> dictJunctions = new();
        foreach (var streetPoint in clusterDesc.StrokeStore().GetStreetPoints())
        {
            NavJunction nj = new()
            {
                Position = streetPoint.Pos3 + clusterDesc.Pos with { Y = navY },
                StartingLanes = new(),
                EndingLanes = new()
            };
            dictJunctions[streetPoint.Id] = nj;
            ncc.Junctions.Add(nj);
        }

        int carLaneCount = 0;
        int skippedStrokes = 0;
        var strokes = clusterDesc.StrokeStore().GetStrokes();

        foreach (var stroke in strokes)
        {
            if (stroke.A.ClusterId != stroke.B.ClusterId)
            {
                Trace($"NavMap {clusterDesc.Name}: Skipping cross-cluster bridge stroke ({stroke.A.ClusterId} → {stroke.B.ClusterId})");
                skippedStrokes++;
                continue;
            }

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
                carLaneCount += _createBidirectionalLanes(njA, njB, TransportationType.Car, ncc);
            }
            catch (Exception e)
            {
                Trace($"Exception adding car navlane in {clusterDesc.Name}: {e}");
                skippedStrokes++;
            }
        }

        /*
         * === Pedestrian Sidewalk Lanes (from Quarter boundaries) ===
         */
        int pedestrianLaneCount = 0;
        int crossingLaneCount = 0;

        // Position-keyed dictionary to deduplicate junctions at shared quarter corners.
        // Key: rounded position (1/10 unit precision) to merge coincident points.
        Dictionary<(int, int), NavJunction> sidewalkJunctions = new();

        // Track which StreetPoint each sidewalk junction belongs to (for crossing generation).
        Dictionary<int, List<NavJunction>> junctionsByStreetPoint = new();

        foreach (var quarter in clusterDesc.QuarterStore().GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            var delims = quarter.GetDelims();
            if (delims.Count < 3) continue;

            // Create or reuse a junction for each quarter corner
            List<NavJunction> quarterJunctions = new();
            for (int di = 0; di < delims.Count; di++)
            {
                var delim = delims[di];
                var key = ((int)(delim.StartPoint.X * 10), (int)(delim.StartPoint.Y * 10));

                if (!sidewalkJunctions.TryGetValue(key, out var nj))
                {
                    nj = new NavJunction
                    {
                        Position = new Vector3(delim.StartPoint.X, 0, delim.StartPoint.Y)
                                   + clusterDesc.Pos with { Y = navY },
                        StartingLanes = new(),
                        EndingLanes = new()
                    };
                    sidewalkJunctions[key] = nj;
                    ncc.Junctions.Add(nj);
                }

                quarterJunctions.Add(nj);

                // Group by StreetPoint for crossing generation
                int spId = delim.StreetPoint.Id;
                if (!junctionsByStreetPoint.TryGetValue(spId, out var list))
                {
                    list = new List<NavJunction>();
                    junctionsByStreetPoint[spId] = list;
                }
                if (!list.Contains(nj))
                {
                    list.Add(nj);
                }
            }

            // Create sidewalk lanes along each quarter edge (wrapping last→first)
            for (int i = 0; i < quarterJunctions.Count; i++)
            {
                var njA = quarterJunctions[i];
                var njB = quarterJunctions[(i + 1) % quarterJunctions.Count];
                if (njA == njB) continue;

                pedestrianLaneCount += _createBidirectionalLanes(njA, njB, TransportationType.Pedestrian, ncc);
            }
        }

        /*
         * === Pedestrian Crossing Lanes (connect sidewalks across streets) ===
         */
        foreach (var (spId, junctions) in junctionsByStreetPoint)
        {
            for (int i = 0; i < junctions.Count; i++)
            {
                for (int j = i + 1; j < junctions.Count; j++)
                {
                    crossingLaneCount += _createBidirectionalLanes(
                        junctions[i], junctions[j], TransportationType.Pedestrian, ncc);
                }
            }
        }

        Trace($"NavMap cluster {clusterDesc.Name}: " +
              $"{dictJunctions.Count} car junctions, {carLaneCount} car lanes, {skippedStrokes}/{strokes.Count} strokes skipped, " +
              $"{sidewalkJunctions.Count} sidewalk junctions, {pedestrianLaneCount} sidewalk lanes, {crossingLaneCount} crossing lanes");

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