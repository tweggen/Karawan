namespace engine;

abstract public class AController : AModule
{

    protected virtual void OnLogicalFrame(object? sender, float dt)
    {
        
    }
    

    protected override void OnModuleDeactivate()
    {
        _engine.OnLogicalFrame -= OnLogicalFrame;
        
        base.OnModuleDeactivate();
    }
    
    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();

        _engine.OnLogicalFrame += OnLogicalFrame;
    }
}