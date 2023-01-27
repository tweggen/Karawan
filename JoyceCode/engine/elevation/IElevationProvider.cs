

namespace engine.elevation
{
    public interface IElevationProvider
    {
        public Rect GetElevationRectBelow(
            float x0, float z0,
            float x1, float z1
        );
    }
}
