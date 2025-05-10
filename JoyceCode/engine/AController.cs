namespace engine;

abstract public class AController : AModule
{

    protected virtual void OnLogicalFrame(object? sender, float dt)
    {
    }
    

    protected override void _internalOnModuleDeactivate()
    {
        _engine.OnLogicalFrame -= OnLogicalFrame;
        
        base._internalOnModuleDeactivate();
    }
    
    protected override void _internalOnModuleActivate()
    {
        base._internalOnModuleActivate();

        _engine.OnLogicalFrame += OnLogicalFrame;
    }
}