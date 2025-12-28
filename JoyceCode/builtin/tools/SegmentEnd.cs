using System.Numerics;
using engine;

namespace builtin.tools;

public class SegmentEnd
{
    public Vector3 Position;
    public Vector3 Up;
    public Vector3 Right;

    /**
     * If available, this contains the semantic position of this
     * routing point.
     */
    public PositionDescription? PositionDescription;
}