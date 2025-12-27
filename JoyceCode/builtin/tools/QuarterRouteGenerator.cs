using System.Numerics;
using engine.streets;
using engine.world;

namespace builtin.tools;

public class QuarterRouteGenerator
{
    public ClusterDesc ClusterDesc { get; set; }
    public Quarter Quarter { get; set; }
    public QuarterDelim QuarterDelim { get; set; }
    
    public SegmentRoute GenerateRoute()
    {
        var sr = new SegmentRoute();
        /*
         * Construct the route from navigation segments.
         */
        var delims = Quarter.GetDelims();
        int l = delims.Count;

        for (int i = 0; i < l; ++i)
        {
            var dlThis = delims[i];
            var dlNext = delims[(i + 1) % l];

            if (QuarterDelim == dlThis)
            {
                sr.StartIndex = i;
            }

            float h = ClusterDesc.AverageHeight + engine.world.MetaGen.ClusterStreetHeight +
                      engine.world.MetaGen.QuarterSidewalkOffset;
            var v3This = new Vector3(dlThis.StartPoint.X, h, dlThis.StartPoint.Y);
            var v3Next = new Vector3(dlNext.StartPoint.X, h, dlNext.StartPoint.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);
            v3This += -1.5f * vu3Right;

            sr.Segments.Add(
                new()
                {
                    Position = v3This + ClusterDesc.Pos,
                    Up = vu3Up,
                    Right = vu3Right
                });
        }

        return sr;
    }
}