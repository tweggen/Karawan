using System.Numerics;
using engine;
using engine.joyce;
using engine.news;
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


    private void _onMousePressed(engine.news.Event ev)
    {
        /*
         * Find out what the user clicked on.
         * Iterate through it from the top camera to the bottom.
         */
    }
    

    protected override void OnModuleDeactivate()
    {
        Props.Set("engine.editor.isOpen", false);

        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onMousePressed);
        
        _engine.OnImGuiRender -= _onImGuiRender;
        _engine.DisableEntityIds();
    }

    
    protected override void OnModuleActivate()
    {
        _engine.OnImGuiRender += _onImGuiRender;
        _engine.EnableEntityIds();
        
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onMousePressed);
        
        Props.Set("engine.editor.isOpen", true);
    }

}