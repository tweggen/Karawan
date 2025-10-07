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

public class WalkTouchButton : AModule
{
    private List<DefaultEcs.Entity> _buttons;
    
    protected override void OnModuleDeactivate()
    {
        I.Get<Engine>().AddDoomedEntities(_buttons);
        _buttons = null;
    }

    
    protected override void OnModuleActivate()
    {        
        _buttons = new();

        if (GlobalSettings.Get("debug.option.forceTouchInterface") == "true"
            || GlobalSettings.Get("splash.touchControls") == "true")
        {
            _buttons.Add(TouchButtons.CreateButton("but_getinout.png", TouchButtons.ButtonsPerRow - 6, TouchButtons.ButtonsPerColumn - 4,
                (entity, ev, pos) => new Event(ev.IsPressed ? Event.INPUT_BUTTON_PRESSED : Event.INPUT_BUTTON_RELEASED,
                    "<change>")));
            _buttons.Add(TouchButtons.CreateButton("but_accel.png", TouchButtons.ButtonsPerRow - 2, TouchButtons.ButtonsPerColumn - 4,
                (entity, ev, pos) =>
                    new Event(ev.IsPressed ? Event.INPUT_BUTTON_PRESSED : Event.INPUT_BUTTON_RELEASED, "<fire>")));

        }
    }
}