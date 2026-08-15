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
        new SharedModule<LayerCatalogue>(),

        /*
         * The rebinding screen lives under this menu, and its capture mode has to be able
         * to swallow raw key events before the menu widgets see them. Tying its lifetime
         * to the pause menu means nothing is intercepting input while the player is
         * actually playing.
         */
        new SharedModule<builtin.controllers.RebindController>()
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
    

    /**
     * The Controls screen shows "press a key..." while capturing, so it has to re-render
     * when the capture ends. The click that STARTS a capture refreshes itself from Lua;
     * nothing else would refresh when the key finally arrives.
     *
     * The hop is not optional. OnCaptureFinished is raised from InputMapper.ToLogical,
     * which runs on the platform's event-pump thread while SDL is still delivering the
     * key - the JT widget tree belongs to the logical thread. This is the same hazard as
     * the NPC strategies that touched an entity from a thread-pool continuation; there it
     * surfaced as an exception, here it would be a torn widget tree, which is worse
     * because it does not announce itself.
     */
    private void _onRebindFinished(string action)
    {
        _engine.RunMainThread(() =>
        {
            if (!IsModuleActive())
            {
                return;
            }

            try
            {
                M<Factory>().CloseOSD(_layerDefinition.Name, "menuControls");
                M<Factory>().OpenOSD("menu.xml", "menuControls");
            }
            catch (Exception e)
            {
                Error($"Exception refreshing the controls menu after rebinding '{action}': {e}");
            }
        });
    }


    protected override void OnModuleDeactivate()
    {
        M<builtin.controllers.RebindController>().OnCaptureFinished = null;

        M<InputEventPipeline>().RemoveInputPart(this);

        M<Factory>().CloseAll(_layerDefinition.Name);

        _engine.DisablePause();
    }


    protected override void OnModuleActivate()
    {
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

        M<builtin.controllers.RebindController>().OnCaptureFinished = _onRebindFinished;

        M<InputEventPipeline>().AddInputPart(_layerDefinition.ZOrder, this);
    }
}
