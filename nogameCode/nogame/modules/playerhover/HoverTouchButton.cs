using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using nogame.modules.osd;

namespace nogame.modules.playerhover;

public class HoverTouchButton : AModule
{
    private List<DefaultEcs.Entity> _buttons;
    
    protected override void OnModuleDeactivate()
    {
        IList<DefaultEcs.Entity> buttons;
        lock (_lo)
        {
            buttons = _buttons;
            _buttons = null;       
        }
        _engine.QueueMainThreadAction(() =>
        {
            foreach (var iterEntity in buttons)
            {
                DefaultEcs.Entity entity = iterEntity;
                I.Get<HierarchyApi>().Delete(ref entity);
            }
        });
    }

    
    protected override void OnModuleActivate()
    {        
        _buttons = new();

        if (GlobalSettings.Get("debug.option.forceTouchInterface") == "true"
            || GlobalSettings.Get("splash.touchControls") == "true")
        {
            _engine.QueueMainThreadAction(() =>
            {
                _buttons.Add(TouchButtons.CreateButton("but_left.png", 
                    0, 
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_KEY_PRESSED : Event.INPUT_KEY_RELEASED, "a")));
                _buttons.Add(TouchButtons.CreateButton("but_right.png", 
                    1, 
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_KEY_PRESSED : Event.INPUT_KEY_RELEASED, "d")));
                _buttons.Add(TouchButtons.CreateButton("but_getinout.png", 
                    TouchButtons.ButtonsPerRow - 2, 
                    TouchButtons.ButtonsPerColumn - 3,
                    (entity, ev, pos) => new Event(
                        ev.IsPressed ? Event.INPUT_BUTTON_PRESSED : Event.INPUT_BUTTON_RELEASED,
                        "<change>")));
                _buttons.Add(TouchButtons.CreateButton("but_accel.png", 
                    TouchButtons.ButtonsPerRow - 2,
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_KEY_PRESSED : Event.INPUT_KEY_RELEASED, "w")));
                _buttons.Add(TouchButtons.CreateButton("but_brake.png", 
                    TouchButtons.ButtonsPerRow - 3,
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_KEY_PRESSED : Event.INPUT_KEY_RELEASED, "s")));
            });
        }
    }
}