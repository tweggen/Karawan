using System.Numerics;
using engine;

namespace builtin.modules.satnav;

public class Module : AModule
{
    public Route ActivateRoute(IWaypoint wFrom, IWaypoint wTo)
    {
        Route route = new Route(wFrom, wTo);
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