using System.Numerics;
using builtin.extensions;
using engine;
using engine.streets;
using engine.world;

namespace builtin.tools;


/**
 * Generate a segment route for any given cluster/quarter combination..
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

        for (int i = 0; i < l; ++i)
        {
            var dlThis = delims[i];
            var dlNext = delims[(i + 1) % l];

            /*
             * The direction is taken in plan, as it always was: both ends used to be given
             * the SAME height so the difference was horizontal anyway.
             */
            var v3This = new Vector3(dlThis.StartPoint.X, 0f, dlThis.StartPoint.Y);
            var v3Next = new Vector3(dlNext.StartPoint.X, 0f, dlNext.StartPoint.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);
            v3This += -1.5f * vu3Right;

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
             * is the pavement's height anywhere along that edge. Same measurement: p05
             * -0.23 m, median 0.00, p95 +0.23. Identical to the pad in a flat city, where
             * every corner is at the average and the interpolation is exact.
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