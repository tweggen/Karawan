using engine;

namespace builtin.modules.satnav;

public class Module : AModule
{
    private MapDB _mapDb = new();
    
    
    /**
     * Create a route from one waypoint to another.
     */
    public Route ActivateRoute(IWaypoint wFrom, IWaypoint wTo)
    {
        Route route = new Route(_mapDb, wFrom, wTo);
        route.LoadMap();
        route.FindRoute();
        return route;
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
    }
}