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
            /*
             * The drive buttons emit the ACTION, not a fake key: a key event needs a
             * ScanCode to resolve against the bindings, and a touch button has none.
             * See Event.INPUT_ACTION_PRESSED.
             */
            _engine.QueueMainThreadAction(() =>
            {
                _buttons.Add(TouchButtons.CreateButton("but_left.png", 
                    0, 
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_ACTION_PRESSED : Event.INPUT_ACTION_RELEASED, "walkleft")));
                _buttons.Add(TouchButtons.CreateButton("but_right.png", 
                    1, 
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_ACTION_PRESSED : Event.INPUT_ACTION_RELEASED, "walkright")));
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
                        new Event(ev.IsPressed ? Event.INPUT_ACTION_PRESSED : Event.INPUT_ACTION_RELEASED, "walkforward")));
                _buttons.Add(TouchButtons.CreateButton("but_brake.png", 
                    TouchButtons.ButtonsPerRow - 3,
                    TouchButtons.ButtonsPerColumn - 2,
                    (entity, ev, pos) =>
                        new Event(ev.IsPressed ? Event.INPUT_ACTION_PRESSED : Event.INPUT_ACTION_RELEASED, "walkbackward")));
            });
        }
    }
}