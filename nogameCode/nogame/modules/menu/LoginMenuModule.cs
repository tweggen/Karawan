using System;
using System.Collections.Generic;
using System.Xml;
using builtin.jt;
using engine;
using engine.news;
using static engine.Logger;

namespace nogame.modules.menu;


public class LoginMenuModule : AModule
{
    public LayerDefinition _layerDefinition;


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.jt.Factory>(),
        new SharedModule<InputEventPipeline>(),
        new SharedModule<LayerCatalogue>()
    };

    
    public override void ModuleDeactivate()
    {
        M<Factory>().CloseOSD(_layerDefinition.Name, "menuLogin");
        _engine.DisablePause();
        
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _engine.EnablePause();

        try
        {
            var wMenu = M<Factory>().OpenOSD("menu.xml", "menuLogin");
            _layerDefinition = M<LayerCatalogue>().Get(wMenu["layer"].ToString());
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }
    }
}
