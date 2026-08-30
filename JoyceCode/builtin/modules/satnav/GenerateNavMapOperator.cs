using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using DefaultEcs;
using engine;
using engine.navigation;
using engine.streets;
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
    private static readonly engine.Dc _dc = engine.Dc.Satnav;

    private const float MaxLaneLength = 50f;


    /**
     * The nav junction on a block's pavement corner.
     *
     * A quarter delimiter is a section corner OF a street junction and carries it, so the
     * exact junction height is available here - no need to sample the terrain near the
     * corner and hope. The pavement then meets the carriageway it runs beside, and the NPC
     * walking over it stands on the kerb the block's floor draws.
     *
     * The delimiter's StreetPoint IS the junction its corner stands on - see QuarterDelim,
     * where the corner, the junction and the stroke are one write. The block floor takes
     * the same junction, which is the point.
     *
     * Level 0 always: quarters are traced on the ground only, so a deck has no pavement to
     * walk on until something generates one.
     *
     * Here rather than inline in the operator because the operator needs a stroke store, a
     * quarter store and the container, and is not exercised - so inline is where the wrong
     * half of the delimiter would never be seen.
     *
     * @param v3ClusterPos
     *     Where the cluster's origin is in the world. Y is ignored.
     */
    internal static NavJunction SidewalkJunctionFor(
        QuarterDelim delim, Vector3 v3ClusterPos, IStreetHeightSource heightSource)
    {
        Vector3 v3Corner = new Vector3(delim.StartPoint.X, 0f, delim.StartPoint.Y)
                           + v3ClusterPos;

        return NavJunction.At(v3Corner, heightSource.GroundHeightAt(delim.StreetPoint));
    }


    /**
     * File a block's pavement corner under the junction it stands on.
     *
     * A crossing spans the carriageway AT a junction, between two of that junction's own
     * section points, so the corners a junction is asked about have to be its own.
     * The junction is the delimiter's own StreetPoint, which is the junction its corner
     * stands on - see QuarterDelim. Filing a corner under the junction at the far end of
     * its block's next edge, which is what this did before both that and the delimiter were
     * corrected, puts it in the list of a junction 70 to 97 m away that it does not touch.
     *
     * Written out here rather than inline because the operator needs a stroke store, a
     * quarter store and the container and is not exercised, and because the height (above)
     * and the crossing filing must name the SAME junction for a corner. Two inline reads of
     * a delimiter is how they came to differ in the first place.
     */
    internal static void FileCornerUnderItsJunction(
        QuarterDelim delim, NavJunction nj,
        IDictionary<int, List<NavJunction>> junctionsByStreetPoint,
        IDictionary<int, StreetPoint> streetPointById)
    {
        StreetPoint sp = delim.StreetPoint;

        if (!junctionsByStreetPoint.TryGetValue(sp.Id, out var list))
        {
            list = new List<NavJunction>();
            junctionsByStreetPoint[sp.Id] = list;
        }

        if (!list.Contains(nj))
        {
            list.Add(nj);
        }

        streetPointById[sp.Id] = sp;
    }


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

                /*
                 * Ground and position together - see NavJunction.Between. An
                 * intermediate junction that kept the position and lost the ground would
                 * look right and drop every walker crossing it.
                 */
                NavJunction njIntermediate = NavJunction.Between(njA, njB, t);
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
        Trace(_dc, $"Loading cluster {clusterDesc.Name}");

        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };

        var heightSource = clusterDesc.StreetHeightSource;

        /*
         * === Car Lanes (from Strokes) ===
         */
        SortedDictionary<int, NavJunction> dictJunctions = new();
        foreach (var streetPoint in clusterDesc.StrokeStore().GetStreetPoints())
        {
            /*
             * A car lane junction IS a street junction, so this is the one place that
             * can ask the height source for the exact answer rather than sampling the
             * terrain near it. Traffic then runs at the height of the road it is on,
             * cut and fill included, instead of near it.
             */
            float groundY = heightSource.GroundHeightAt(streetPoint)
                            + streetPoint.LevelElevation;

            /*
             * Navigation is world space, so unlike StreetPoint.Pos3 - which is the planar
             * octree key - this carries the junction's deck height.
             *
             * Two things then follow on their own, because lanes measure themselves with
             * Vector3.Distance and split themselves with Vector3.Lerp: a ramp's length is
             * its true sloped length rather than its plan length, so routing cannot get a
             * discount for climbing, and a long ramp's intermediate junctions land part
             * way up it. A street running downhill now gets the same treatment for the
             * same reason.
             */
            NavJunction nj = NavJunction.At(streetPoint.Pos3 + clusterDesc.Pos, groundY);
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
                Trace(_dc, $"NavMap {clusterDesc.Name}: Skipping cross-cluster bridge stroke ({stroke.A.ClusterId} → {stroke.B.ClusterId})");
                skippedStrokes++;
                continue;
            }

            if (!dictJunctions.TryGetValue(stroke.A.Id, out var njA))
            {
                Trace(_dc, $"NavMap {clusterDesc.Name}: Stroke missing start junction {stroke.A.Id}");
                skippedStrokes++;
                continue;
            }
            if (!dictJunctions.TryGetValue(stroke.B.Id, out var njB))
            {
                Trace(_dc, $"NavMap {clusterDesc.Name}: Stroke missing end junction {stroke.B.Id}");
                skippedStrokes++;
                continue;
            }

            try
            {
                carLaneCount += _createBidirectionalLanes(njA, njB, TransportationType.Car, ncc);
            }
            catch (Exception e)
            {
                Trace(_dc, $"Exception adding car navlane in {clusterDesc.Name}: {e}");
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

        /*
         * Which junction's pavement corners are which, for crossing generation.
         *
         * Sorted by junction id, as the car-lane junctions above are, so the order the
         * crossings come out in is a property of the cluster rather than of which block
         * the quarter store happened to trace first. A Dictionary would enumerate in
         * insertion order, which changes with the filing.
         */
        SortedDictionary<int, List<NavJunction>> junctionsByStreetPoint = new();
        SortedDictionary<int, StreetPoint> streetPointById = new();

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
                    nj = SidewalkJunctionFor(delim, clusterDesc.Pos, heightSource);
                    sidewalkJunctions[key] = nj;
                    ncc.Junctions.Add(nj);
                }

                quarterJunctions.Add(nj);

                FileCornerUnderItsJunction(
                    delim, nj, junctionsByStreetPoint, streetPointById);
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
         * === Pedestrian Crossing Lanes ===
         * Create one crossing per arm at each junction:
         *   - Skip 2-arm junctions (straight roads, no crossing needed)
         *   - For 1-arm (dead-end): connect all sidewalk corners across the tip
         *   - For 3+ arms: one perpendicular crossing per arm, using the two
         *     section points that flank the arm (from StreetPoint.GetSectionPointByStroke)
         */
        foreach (var (spId, junctions) in junctionsByStreetPoint)
        {
            var sp = streetPointById[spId];
            var arms = sp.GetAngleArray();
            int n = arms.Count;

            if (n == 0) continue;

            // Req 1: skip 2-arm (straight road), no crossing
            if (n == 2) continue;

            // 1-arm dead-end: just connect all sidewalk corners (usually 2)
            if (n == 1)
            {
                for (int i = 0; i < junctions.Count; i++)
                    for (int j = i + 1; j < junctions.Count; j++)
                        crossingLaneCount += _createBidirectionalLanes(
                            junctions[i], junctions[j], TransportationType.Pedestrian, ncc);
                continue;
            }

            // 3+ arms: one perpendicular crossing per arm
            for (int i = 0; i < n; i++)
            {
                var curr = arms[i];
                var prev = arms[(i - 1 + n) % n];
                var next = arms[(i + 1) % n];

                // Section points flanking this arm
                var ptA = sp.GetSectionPointByStroke(curr, prev);   // right side of arm
                var ptB = sp.GetSectionPointByStroke(next, curr);   // left side of arm

                if (ptA == null || ptB == null) continue;

                // Look up the pre-built NavJunctions via the position-keyed dictionary
                var keyA = ((int)(ptA.Value.X * 10), (int)(ptA.Value.Y * 10));
                var keyB = ((int)(ptB.Value.X * 10), (int)(ptB.Value.Y * 10));

                if (!sidewalkJunctions.TryGetValue(keyA, out var njA)) continue;
                if (!sidewalkJunctions.TryGetValue(keyB, out var njB)) continue;
                if (njA == njB) continue;  // degenerate (collinear arms)

                crossingLaneCount += _createBidirectionalLanes(
                    njA, njB, TransportationType.Pedestrian, ncc);
            }
        }

        Trace(_dc, $"NavMap cluster {clusterDesc.Name}: " +
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

            Trace(_dc, $"NavMap cluster {clusterDesc.Name}: {reachable.Count}/{ncc.Junctions.Count} junctions reachable from junction 0");
            if (reachable.Count < ncc.Junctions.Count)
            {
                Trace(_dc, $"  ⚠️ DISCONNECTED: {ncc.Junctions.Count - reachable.Count} junctions unreachable (isolated components)");
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
        Trace(_dc, $"Loading top level cluster");

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
        

        Trace(_dc, $"GenerateNavMapOperator: Done.");
    });
    

    public GenerateNavMapOperator()
    {
    }
}