namespace builtin.modules.satnav;

public class Route
{
    private IWaypoint _a;
    private IWaypoint _b;
    
    public IWaypoint A
    {
        get
        {
            return _a;
        }
    }

    public IWaypoint B
    {
        get
        {
            return _b;
        }
    }

    public Route(IWaypoint a, IWaypoint b)
    {
        _a = a;
        _b = b;
    }
}