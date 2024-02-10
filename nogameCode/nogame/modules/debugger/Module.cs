using System.Numerics;
using engine;
using engine.joyce;
using static engine.Logger;

namespace nogame.modules.debugger;


/**
 * Activates the debug screen.
 */
public class Module : AModule
{
    // For now, let it use the OSD camera.
    public uint MenuCameraMask = 0x01000000;

    
    private void _onImGuiRender(object? sender, float dt)
    {
        I.Get<joyce.ui.Main>().Render(dt);
    }
    

    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnImGuiRender -= _onImGuiRender;
        _engine.DisableEntityIds();
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        _engine.OnImGuiRender += _onImGuiRender;
        _engine.EnableEntityIds();
    }

}