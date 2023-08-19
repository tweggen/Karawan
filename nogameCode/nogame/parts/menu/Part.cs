using System.Numerics;
using engine;
using engine.joyce;
using static engine.Logger;

namespace nogame.parts.menu;


/**
 * Scene part "map" for the main gameplay scene.
 *
 * Displays a full-screen map, using the OSD camera.
 *
 * On activation, makes the map entities visible
 */
public class Part : IPart
{
    private object _lo = new();

    private engine.Engine _engine;
    
    // For now, let it use the OSD camera.
    public uint MenuCameraMask = 0x00010000;

    private engine.joyce.Material _materialMiniMap;

    private void _onImGuiRender(object? sender, float dt)
    {
        Implementations.Get<joyce.ui.Main>().Render(dt);
    }
    
    public void PartOnInputEvent(engine.news.Event keyEvent)
    {
        /*
         * Nothing to do yet.
         */
    }
    

    public void PartDeactivate()
    {
        _engine.RemovePart(this);
        _engine.OnImGuiRender -= _onImGuiRender;
        _engine.DisableEntityIds();
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _engine = engine0;
        // _engine.GetATransform().SetVisible(_eMap, true);
        _engine.AddPart(1000, scene0, this);
        _engine.OnImGuiRender += _onImGuiRender;
        _engine.EnableEntityIds();
    }

}