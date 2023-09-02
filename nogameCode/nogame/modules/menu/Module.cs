using System.Numerics;
using engine;
using engine.joyce;
using static engine.Logger;

namespace nogame.modules.menu;


/**
 * Scene part "map" for the main gameplay scene.
 *
 * Displays a full-screen map, using the OSD camera.
 *
 * On activation, makes the map entities visible
 */
public class Module : AModule
{
    private object _lo = new();

    private engine.Engine _engine;

    // For now, let it use the OSD camera.
    public uint MenuCameraMask = 0x00010000;

    
    private void _onImGuiRender(object? sender, float dt)
    {
        Implementations.Get<joyce.ui.Main>().Render(dt);
    }
    

    public void Dispose()
    {
    }


    public void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnImGuiRender -= _onImGuiRender;
        _engine.DisableEntityIds();
    }

    
    public void ModuleActivate(Engine engine0)
    {
        _engine = engine0;
        _engine.AddModule(this);
        _engine.OnImGuiRender += _onImGuiRender;
        _engine.EnableEntityIds();
    }

}