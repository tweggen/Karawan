namespace builtin.modules.satnav;

public class Route
{
    public MapDB MapDB { get; }

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


    public void LoadMap()
    {
    }


    public void FindRoute()
    {
    }
    
    
    public Route(MapDB mapDB, IWaypoint a, IWaypoint b)
    {
        MapDB = mapDB;
        _a = a;
        _b = b;
    }
}