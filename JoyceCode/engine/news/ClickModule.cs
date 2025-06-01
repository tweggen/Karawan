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
public class ClickModule : AModule
{
    private object _lo = new();
    
    private ClickableHandler _clickableHandler;

    
    private void _handleClickEvent(Event ev)
    {
        _clickableHandler.OnClick(new Event(Event.INPUT_LOGICAL_PRESSED, ev.Code)
        {
            Position = ev.Position,
            Size = ev.Size,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        });
    }


    private void _handleReleaseEvent(Event ev)
    {
        _clickableHandler.OnRelease(new Event(Event.INPUT_LOGICAL_RELEASED, ev.Code)
        {
            Position = ev.Position,
            Size = ev.Size,
            Data1 = ev.Data1,
            Data2 = ev.Data2,
            Data3 = ev.Data3,
        });
    }


    private void _onMousePress(Event ev)
    {
        if (GlobalSettings.Get("Android") != "true")
        {
            _handleClickEvent(ev);
        }
    }
    

    private void _onTouchPress(Event ev)
    {
        if (GlobalSettings.Get("Android") == "true")
        {
            _handleClickEvent(ev);
        }
    }
    
    
    private void _onFingerPress(Event ev)
    {
        if (GlobalSettings.Get("Android") == "true")
        {
            _handleClickEvent(ev);
        }
    }
    
    
    private void _onMouseReleased(Event ev)
    {
        if (GlobalSettings.Get("Android") != "true")
        {
            _handleReleaseEvent(ev);
        }
    }
    

    private void _onTouchReleased(Event ev)
    {
        if (GlobalSettings.Get("Android") == "true")
        {
            _handleReleaseEvent(ev);
        }
    }
    
    
    private void _onFingerReleased(Event ev)
    {
        if (GlobalSettings.Get("Android") == "true")
        {
            _handleReleaseEvent(ev);
        }
    }
    
    
    protected override void OnModuleDeactivate()
    {
        // I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_FINGER_PRESSED, _onFingerPress);
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_MOUSE_PRESSED, _onMousePress);
        // I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_RELEASED, _onTouchReleased);
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_RELEASED, _onFingerReleased);
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_MOUSE_RELEASED, _onMouseReleased);
    }
    
    
    protected override void OnModuleActivate()
    {
        /*
         * Setup osd interaction handler
         */
        _clickableHandler = new(_engine);
        
        // I.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_FINGER_PRESSED, _onFingerPress);
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_MOUSE_PRESSED, _onMousePress);
        // I.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_RELEASED, _onTouchReleased);
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_FINGER_RELEASED, _onFingerReleased);
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_MOUSE_RELEASED, _onMouseReleased);
    }

}