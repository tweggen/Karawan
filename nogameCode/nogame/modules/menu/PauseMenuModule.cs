using System;
using System.Collections.Generic;
using System.Xml;
using builtin.jt;
using engine;
using engine.news;
using static engine.Logger;

namespace nogame.modules.menu;


public class PauseMenuModule : AModule, IInputPart
{
    public LayerDefinition _layerDefinition;


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.jt.Factory>(),
        new SharedModule<InputEventPipeline>(),
        new SharedModule<LayerCatalogue>()
    };

    
    /**
     * This input handler exists purely for closing the osd again. using escape.
     */
    public void InputPartOnInputEvent(Event ev)
    {
        bool doDeactivate = false;

        switch (ev.Type)
        {
            case Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<menu>":
                        doDeactivate = true;
                        break;
                }
                break;
        }


        if (doDeactivate)
        {
            ev.IsHandled = true;
            ModuleDeactivate();
        }

    }
    

    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);

        M<Factory>().CloseAll(_layerDefinition.Name);
        
        _engine.DisablePause();
    }


    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        _engine.EnablePause();

        try
        {
            var wMenu = M<Factory>().OpenOSD("menu.xml", "menuOptions");
            _layerDefinition = M<LayerCatalogue>().Get(wMenu["layer"].ToString());
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }

        M<InputEventPipeline>().AddInputPart(_layerDefinition.ZOrder, this);
    }
}
