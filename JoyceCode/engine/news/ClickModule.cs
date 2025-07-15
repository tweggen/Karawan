using System.Collections.Generic;
using static engine.Logger;
using engine.behave.systems;

namespace engine.news;


/**
 * Once activated, this module subscribes for click/touch events.
 * Whenever the user clicks/touches, the clickable handler scans
 * through the clickable objects to find the matching object, calling
 * it's event factory to create the event desired by the owner of
 * the object.
 *
 * It emits logical pressed / moved / released events.
 */
public class ClickModule : AModule, engine.IInputPart
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>()    
    };
    
    private bool _trace = false;
    private float MY_Z_ORDER = 20f;
    
    private object _lo = new();
    
    private ClickableHandler _clickableHandler;

    
    private void _handleClickEvent(Event ev)
    {
        if (_trace) Trace($"Handling {ev}");
        
        _clickableHandler.OnClick(new Event(Event.INPUT_LOGICAL_PRESSED, ev.Code)
        {
            PhysicalPosition = ev.PhysicalPosition,
            PhysicalSize = ev.PhysicalSize,
            LogicalPosition = ev.LogicalPosition,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        });
    }


    private void _handleReleaseEvent(Event ev)
    {
        if (_trace) Trace($"Handling {ev}");
        
        _clickableHandler.OnRelease(new Event(Event.INPUT_LOGICAL_RELEASED, ev.Code)
        {
            PhysicalPosition = ev.PhysicalPosition,
            PhysicalSize = ev.PhysicalSize,
            LogicalPosition = ev.LogicalPosition,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        });
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") == "false")
        {
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED))
            {
                _handleClickEvent(ev);
    
            }
            else if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED))
            {
                _handleReleaseEvent(ev);
            }
        }
        else
        {
            if (ev.Type.StartsWith(Event.INPUT_FINGER_PRESSED))
            {
                _handleClickEvent(ev);
            } 
            else if (ev.Type.StartsWith(Event.INPUT_FINGER_RELEASED))
            {
                _handleReleaseEvent(ev);
            }
            
        }
    }


    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
    }
    
    
    protected override void OnModuleActivate()
    {
        /*
         * Setup osd interaction handler
         */
        _clickableHandler = new(_engine);

        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }

}