

namespace engine.quest;


/**
 * Might define a quest tracker. Does not. 
 */
public class ToLocation : AModule
{
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        engine0.AddModule(this);

    }
}
