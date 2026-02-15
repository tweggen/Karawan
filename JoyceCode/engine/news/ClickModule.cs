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

        var logicalEv = new Event(Event.INPUT_LOGICAL_PRESSED, ev.Code)
        {
            PhysicalPosition = ev.PhysicalPosition,
            PhysicalSize = ev.PhysicalSize,
            LogicalPosition = ev.LogicalPosition,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        };
        _clickableHandler.OnClick(logicalEv);
        if (logicalEv.IsHandled)
        {
            ev.IsHandled = true;
        }
    }


    private void _handleReleaseEvent(Event ev)
    {
        if (_trace) Trace($"Handling {ev}");

        var logicalEv = new Event(Event.INPUT_LOGICAL_RELEASED, ev.Code)
        {
            PhysicalPosition = ev.PhysicalPosition,
            PhysicalSize = ev.PhysicalSize,
            LogicalPosition = ev.LogicalPosition,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        };
        _clickableHandler.OnRelease(logicalEv);
        if (logicalEv.IsHandled)
        {
            ev.IsHandled = true;
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") == "false")
        {
            /*
             * There is no click on non-touch inputs.
             */
            #if false
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED))
            {
                _handleClickEvent(ev);
    
            }
            else if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED))
            {
                _handleReleaseEvent(ev);
            }
            #endif
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