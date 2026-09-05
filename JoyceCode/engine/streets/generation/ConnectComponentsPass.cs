using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using static engine.Logger;

namespace engine.streets.generation;


/**
 * Bridges disconnected street bundles back onto the main network.
 *
 * Runs once, after the candidate queue has drained. Moved out of Generator verbatim
 * in WP-2c; it is emphatically not dead code, despite reading like a safety net -
 * measured over 180 generated clusters, 105 of them needed at least one bridge.
 *
 * The corridor branch is a different matter: across those same 180 clusters it fired
 * exactly once. It is covered by the seed017@2400 determinism seed and by nothing
 * else, so do not drop that seed.
 *
 * WARNING: _createBridgeCorridor draws from the RandomSource. This pass therefore has
 * to run at the same point in the sequence of draws as it always has, which is at the
 * very end of a run.
 */
internal sealed class ConnectComponentsPass
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    private readonly StrokeStore _strokeStore;
    private readonly NetworkBuilder _networkBuilder;
    private readonly int _clusterId;
    private readonly RandomSource _rnd;
    private readonly string _annotation;


    internal ConnectComponentsPass(
        StrokeStore strokeStore, NetworkBuilder networkBuilder,
        int clusterId, RandomSource rnd, string annotation)
    {
        _strokeStore = strokeStore;
        _networkBuilder = networkBuilder
            ?? throw new ArgumentNullException(nameof(networkBuilder));
        _clusterId = clusterId;
        _rnd = rnd;
        _annotation = annotation;
    }


    private void _trace(string message)
    {
        Trace(_dc, $"{_annotation}: {message}");
    }


    /**
     * Entry point.
     */
    internal void Run()
    {
        _connectOrphanedBundles();
    }


    /// <summary>
    /// Post-processing: Connect orphaned street bundles to the main cluster.
    /// Finds disconnected components and bridges them with connector streets.
    /// </summary>
    private void _connectOrphanedBundles()
    {
        var allPoints = _strokeStore.GetStreetPoints().ToList();
        if (allPoints.Count == 0) return;

        // Find all connected components via BFS on stroke graph
        var components = _findConnectedComponents(allPoints);
        if (components.Count <= 1)
        {
            _trace($"All streets connected - no orphaned bundles to bridge.");
            return;
        }

        _trace($"\n= ORPHANED BUNDLE BRIDGING =======================");
        _trace($"Found {components.Count} disconnected street bundles");

        // Get the largest component (main city)
        var mainComponent = components.OrderByDescending(c => c.Count).First();
        var mainPointIds = new HashSet<int>(mainComponent.Select(p => p.Id));

        _trace($"Main cluster: {mainComponent.Count} streets");

        // Connect each orphan to the main cluster
        int bridgeCount = 0;
        foreach (var orphanComponent in components.Skip(1))
        {
            _trace($"Connecting orphan bundle ({orphanComponent.Count} streets)...");

            if (_bridgeOrphanToMain(orphanComponent, mainComponent, mainPointIds))
            {
                bridgeCount++;
            }
        }

        _trace($"Created {bridgeCount} bridge connections to main cluster.");
    }

    /// <summary>
    /// Find connected components of streets using BFS.
    /// </summary>
    private List<List<StreetPoint>> _findConnectedComponents(List<StreetPoint> allPoints)
    {
        var pointDict = allPoints.ToDictionary(p => p.Id, p => p);
        var allStrokes = _strokeStore.GetStrokes().ToList();

        // Build adjacency for BFS
        var adj = new Dictionary<int, List<int>>();
        foreach (var point in allPoints)
            adj[point.Id] = new List<int>();

        foreach (var stroke in allStrokes)
        {
            adj[stroke.A.Id].Add(stroke.B.Id);
            adj[stroke.B.Id].Add(stroke.A.Id);
        }

        // BFS to find components
        var visited = new HashSet<int>();
        var components = new List<List<StreetPoint>>();

        foreach (var point in allPoints)
        {
            if (visited.Contains(point.Id)) continue;

            var component = new List<StreetPoint>();
            var queue = new Queue<int>();
            queue.Enqueue(point.Id);
            visited.Add(point.Id);

            while (queue.Count > 0)
            {
                int curr = queue.Dequeue();
                component.Add(pointDict[curr]);

                foreach (int neighbor in adj[curr])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            components.Add(component);
        }

        return components.OrderByDescending(c => c.Count).ToList();
    }

    /// <summary>
    /// Bridge a single orphaned component to the main cluster.
    /// </summary>
    private bool _bridgeOrphanToMain(List<StreetPoint> orphan, List<StreetPoint> main, HashSet<int> mainPointIds)
    {
        // Find convex hull perimeter of orphan
        var orphanHull = _getConvexHull(orphan);
        if (orphanHull.Count == 0) return false;

        // Find hull point closest to main cluster
        StreetPoint bestOrphanPoint = null;
        float bestDistance = float.MaxValue;

        foreach (var hullPoint in orphanHull)
        {
            float minDist = float.MaxValue;
            foreach (var mainPoint in main)
            {
                /*
                 * Distance here is PLAN distance, and two junctions on different decks
                 * can be stacked exactly on top of one another - that stacking is what
                 * an overpass is. Joining them would be a road climbing eight metres
                 * through the air over no distance at all, which NetworkBuilder refuses
                 * outright; so a candidate on another deck is not a candidate.
                 *
                 * Every junction of a ground-only city is on level 0, so this admits
                 * exactly the set it always did there.
                 */
                if (mainPoint.Level != hullPoint.Level) continue;

                float dist = Vector2.Distance(hullPoint.Pos, mainPoint.Pos);
                if (dist < minDist)
                    minDist = dist;
            }

            if (minDist < bestDistance)
            {
                bestDistance = minDist;
                bestOrphanPoint = hullPoint;
            }
        }

        if (bestOrphanPoint == null) return false;

        // Find closest point on main cluster
        StreetPoint bestMainPoint = null;
        float minMainDist = float.MaxValue;

        foreach (var mainPoint in main)
        {
            if (mainPoint.Level != bestOrphanPoint.Level) continue;

            float dist = Vector2.Distance(mainPoint.Pos, bestOrphanPoint.Pos);
            if (dist < minMainDist && !orphan.Contains(mainPoint))
            {
                minMainDist = dist;
                bestMainPoint = mainPoint;
            }
        }

        if (bestMainPoint == null)
        {
            /*
             * Refusing to bridge leaves the orphan disconnected, which is visible and
             * survivable; guessing a partner on another deck is not. Reported rather
             * than traced: a bundle that never reattaches is a real defect in whatever
             * ruleset produced it, and a Trace is filtered off by default.
             */
            Warning(_dc,
                $"{_annotation}: no junction on level {bestOrphanPoint.Level} to bridge "
                + $"an orphan bundle of {orphan.Count} junctions to; leaving it "
                + $"disconnected.");
            return false;
        }

        // Create bridge stroke(s)
        float bridgeDistance = Vector2.Distance(bestOrphanPoint.Pos, bestMainPoint.Pos);

        if (bridgeDistance > 300f)
        {
            // Long distance: create multi-stroke corridor
            _createBridgeCorridor(bestOrphanPoint, bestMainPoint);
            _trace($"  Bridged (corridor) at distance {bridgeDistance:F1}m");
        }
        else
        {
            // Short distance: single direct stroke
            _createBridgeStroke(bestOrphanPoint, bestMainPoint);
            _trace($"  Bridged (direct) at distance {bridgeDistance:F1}m");
        }

        return true;
    }

    /// <summary>
    /// Create a direct bridge stroke between two points.
    /// </summary>
    private void _createBridgeStroke(StreetPoint fromPoint, StreetPoint toPoint)
    {
        var bridge = new Stroke
        {
            A = fromPoint,
            B = toPoint,
            ClusterId = _clusterId,
            IsPrimary = false,
            Weight = 0.7f,  // Secondary/suburban roads
            Kind = StrokeKind.ConnectorBridge
        };
        bridge.PushCreator("orphan_bridge");

        /*
         * Through NetworkBuilder rather than straight into the store. This pass used to
         * be the one place that added strokes without the level rules ever being
         * consulted.
         */
        _networkBuilder.Commit(bridge);
    }

    /// <summary>
    /// Create a curved multi-stroke corridor for long bridge distances.
    /// </summary>
    private void _createBridgeCorridor(StreetPoint fromPoint, StreetPoint toPoint)
    {
        var from = fromPoint.Pos;
        var to = toPoint.Pos;
        var mid = (from + to) / 2f;

        // Add perpendicular offset for curve
        var delta = to - from;
        var perpendicular = new Vector2(-delta.Y, delta.X);
        perpendicular = Vector2.Normalize(perpendicular);

        float offset = 40f + _rnd.GetFloat() * 40f;  // Random curve amount
        mid += perpendicular * offset;

        /*
         * Create intermediate point. On the deck its two ends are on: both ends of a
         * corridor are on the same level by construction (the caller only pairs
         * junctions that are), and a mid point left on the default level 0 would turn
         * each half of a level-1 corridor into a stroke joining two decks.
         *
         * NOTE `mid` above is computed - the offset draw included - and never assigned
         * to this point, so the corridor's middle junction sits at the CLUSTER ORIGIN.
         * Measured on seed017@2400, the only one of 180 clusters that reaches this
         * branch: a 318 m gap is bridged by 1341.7 m + 1050.3 m through the middle of
         * the city. Pre-existing and deliberately NOT fixed in WP-B1, because
         * SetPos-ing the point moves that cluster's recorded fingerprint and this work
         * package may not move the default city.
         */
        var midPoint = new StreetPoint { ClusterId = _clusterId, Level = fromPoint.Level };
        midPoint.PushCreator("corridor_mid");

        // Create two segments: from→mid, mid→to
        var seg1 = new Stroke
        {
            A = fromPoint,
            B = midPoint,
            ClusterId = _clusterId,
            IsPrimary = false,
            Weight = 0.7f,
            Kind = StrokeKind.ConnectorBridge
        };
        seg1.PushCreator("corridor_seg1");

        var seg2 = new Stroke
        {
            A = midPoint,
            B = toPoint,
            ClusterId = _clusterId,
            IsPrimary = false,
            Weight = 0.7f,
            Kind = StrokeKind.ConnectorBridge
        };
        seg2.PushCreator("corridor_seg2");

        /*
         * CommitChain adds them in list order, so the store and both octrees see
         * exactly the sequence they always have; what it adds is that neither half goes
         * in unless both are admissible.
         */
        _networkBuilder.CommitChain(new List<Stroke> { seg1, seg2 });
    }

    /// <summary>
    /// Get the convex hull of points (simplified: extremal points).
    /// </summary>
    private List<StreetPoint> _getConvexHull(List<StreetPoint> points)
    {
        if (points.Count <= 3) return points;

        // Simplified hull: return extremal points
        var hull = new List<StreetPoint>();

        // Leftmost
        hull.Add(points.OrderBy(p => p.Pos.X).First());
        // Rightmost
        hull.Add(points.OrderBy(p => p.Pos.X).Last());
        // Topmost
        hull.Add(points.OrderBy(p => p.Pos.Y).First());
        // Bottommost
        hull.Add(points.OrderBy(p => p.Pos.Y).Last());

        // Remove duplicates and return
        return hull.Distinct().ToList();
    }
}
