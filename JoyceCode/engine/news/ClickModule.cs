using engine.behave.systems;

namespace engine.news;


public class ClickModule : AModule
{
    private object _lo = new();
    
    private ClickableHandler _clickableHandler;

    private void _handleClickEvent(Event ev)
    {
        _clickableHandler.OnClick(ev);
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
    
    
    public override void ModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_MOUSE_PRESSED, _onMousePress);

        _engine.RemoveModule(this);
        
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        
        /*
         * Setup osd interaction handler
         */
        _clickableHandler = new(_engine);
        
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_MOUSE_PRESSED, _onMousePress);
    }

}