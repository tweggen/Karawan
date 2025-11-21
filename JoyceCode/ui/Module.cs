using System.Numerics;
using engine;
using engine.joyce;
using engine.news;
using static engine.Logger;

namespace joyce.ui;


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


    protected override void OnModuleDeactivate()
    {
        Props.Set("engine.editor.isOpen", false);

        _engine.OnImGuiRender -= _onImGuiRender;
        _engine.DisableEntityIds();
    }

    
    protected override void OnModuleActivate()
    {
        _engine.OnImGuiRender += _onImGuiRender;
        _engine.EnableEntityIds();
        
        Props.Set("engine.editor.isOpen", true);
    }

}