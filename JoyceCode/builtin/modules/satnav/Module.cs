using System.Numerics;
using engine;

namespace builtin.modules.satnav;

public class Module : AModule
{
    public Route ActivateRoute(Waypoint wFrom, Waypoint wTo)
    {
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