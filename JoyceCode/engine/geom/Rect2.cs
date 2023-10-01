using System.Numerics;

namespace engine.geom;

public struct Rect2
{
    public Vector2 A, B;

    public bool Contains(float x, float y)
    {
        return x >= A.X && x <= B.X && y >= A.Y && y <= B.Y;
    }
}