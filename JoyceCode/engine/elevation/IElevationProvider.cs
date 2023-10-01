

using System.Dynamic;

namespace engine.elevation
{
    public interface IElevationProvider
    {
        public ElevationSegment GetElevationSegmentBelow(in geom.Rect2 rect2);
    }
}
