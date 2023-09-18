using SharpNav.Geometry;

namespace Joyce.builtin.tools;

public class SegmentEnd
{
    public Vector3 Position;
    public Vector3 Up;
    public Vector3 Right;
}

/**
 * Implement a navigator for a run between two points.
 * Can take several objects that shall be navigated along that way.
 */
public class SegmentNavigator
{
    private SegmentEnd _a;
    public SegmentEnd A
    {
        get => _a;
    }
    private SegmentEnd _b;

    public SegmentEnd B
    {
        get => _b;
    }


    public void NavigatorBehave(float dt)
    {
    }
    
    
    public SegmentNavigator(SegmentEnd a, SegmentEnd b)
    {
        _a = a;
        _b = b;
    }
}