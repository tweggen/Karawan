using System.Collections.Generic;
using System.Numerics;
using builtin.extensions;
using engine;
using engine.streets;
using engine.world;

namespace builtin.tools;


/**
 * Generate a segment route for any given cluster/quarter combination..
 *
 * One segment per block corner, in the order the block was traced, so that a segment's
 * index into this route IS its index into Quarter.GetDelims(). SegmentNavigator relies on
 * that identity in both directions - it starts from PositionDescription.QuarterDelimIndex
 * and it writes the delimiter back by indexing GetDelims() with a SEGMENT index - so a
 * route with two waypoints per edge does not merely renumber the walk, it indexes a
 * delimiter list of n with a number up to 2n-1. That is why the walk is one point per
 * corner and not the pavement's own inset ring, which has two points per edge.
 */
public class QuarterLoopRouteGenerator
{
    public required ClusterDesc ClusterDesc { get; set; }
    public required Quarter Quarter { get; set; }

    
    public SegmentRoute GenerateRoute()
    {
        var sr = new SegmentRoute()
        {
            LoopSegments = true
        };

        if (null == Quarter)
        {
            int a = 1;
        }
        
        /*
         * Construct the route from navigation segments.
         */
        var delims = Quarter.GetDelims();
        int l = delims.Count;

        /*
         * Where on the pavement the walk runs.
         *
         * This used to be one corner plus 1.5 m along the inward normal of the edge LEAVING
         * it, which is inside the block exactly when the interior angle exceeds 90 degrees -
         * and the median block corner is 90.1 to 94.0 degrees, so about half of every
         * citizen's waypoints stood in the carriageway. See
         * engine.streets.generation.PavementWalk, which takes both of a corner's edges and
         * is inside by construction, at the block's OWN pavement width rather than a
         * constant that a narrow pavement cannot hold.
         */
        var corners = new List<Vector2>(l);
        foreach (var delim in delims) corners.Add(delim.StartPoint);

        var walk = engine.streets.generation.PavementWalk.RingOf(
            corners, Quarter.SidewalkWidth);

        for (int i = 0; i < l; ++i)
        {
            var dlThis = delims[i];

            /*
             * A ring with no inside at all - fewer than three corners, or zero area - has
             * nowhere to stand off the kerb, so the walk is the kerb.
             */
            Vector2 pThis = null != walk ? walk[i] : corners[i];
            Vector2 pNext = null != walk ? walk[(i + 1) % l] : corners[(i + 1) % l];

            /*
             * The direction is taken in plan, as it always was: both ends used to be given
             * the SAME height so the difference was horizontal anyway.
             */
            var v3This = new Vector3(pThis.X, 0f, pThis.Y);
            var v3Next = new Vector3(pNext.X, 0f, pNext.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);

            /*
             * The height of the PAVEMENT at the waypoint, not of the block's pad at its
             * corner.
             *
             * Quarter.GroundHeightAt is a least squares plane through the block's corner
             * heights, and a block is up to 150 m across with 13 m between its highest and
             * lowest corner - so the plane is the surface only in the middle, where nobody
             * walks, and parts company from it at the kerb, where this walker does. Measured
             * at the loop's own waypoints over the four baseline cities on the shipped
             * terrain, the pad put the ordinary city citizen between 17.8 m below the block
             * floor and 17.0 m above it, and below it at 45 to 60 % of corners.
             *
             * BuildingFooting.GroundAt answers from the boundary edge nearest the point,
             * interpolated between its two corners' own junction heights - which since §7k
             * is the pavement's height anywhere along that edge. Identical to the pad in a
             * flat city, where every corner is at the average and the interpolation is
             * exact.
             *
             * Re-measured against the block floor's own triangles now that the waypoint is
             * on the corner's mitre rather than 1.5 m past it: p05 -0.17 to -0.28 m, median
             * 0.00, p95 +0.12 to +0.29. §7m recorded p05 -0.23 for the old waypoint, but
             * over the 59 to 70 % of waypoints that landed on a block floor AT ALL - the
             * rest were over the road, where there is no floor to be off. Every waypoint is
             * on one now. The residual that is left is §7k's corner ramp, where the cap's
             * surface is the block interior's rather than the rim's; taking the corner's own
             * junction height instead was measured at the same points and is worse
             * (p05 -0.44 to -0.60).
             */
            float h = engine.streets.generation.BuildingFooting.PavementHeightAt(
                Quarter, new Vector2(v3This.X, v3This.Z));
            v3This.Y = h;
            var pod = new PositionDescription()
            {
                ClusterDesc = ClusterDesc,
                
                Quarter = Quarter,
                
                QuarterDelimIndex = i,
                QuarterDelimPos = 0f,
                QuarterDelim = dlThis,

                StreetPoint = dlThis.StreetPoint,
                
                Stroke = dlThis.Stroke,
                
                Position = v3This,
                Orientation = Quaternion.CreateFromRotationMatrix(
                    Matrix4x4Extensions.CreateFromUnitAxis(vu3Right, vu3Up, vu3Forward)),
            };
            
            sr.Segments.Add(
                new()
                {
                    Position = v3This + ClusterDesc.Pos,
                    Up = vu3Up,
                    Right = vu3Right,
                    PositionDescription = pod
                });
        }

        return sr;
    }
}