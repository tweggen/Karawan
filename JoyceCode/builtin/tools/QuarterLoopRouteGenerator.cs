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

            float h = ClusterDesc.AverageHeight + engine.world.MetaGen.ClusterStreetHeight +
                      engine.world.MetaGen.QuarterSidewalkOffset;
            var v3This = new Vector3(dlThis.StartPoint.X, h, dlThis.StartPoint.Y);
            var v3Next = new Vector3(dlNext.StartPoint.X, h, dlNext.StartPoint.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);
            v3This += -1.5f * vu3Right;
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