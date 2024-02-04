
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text.Json;
using System.Timers;
using builtin.controllers;
using builtin.map;
using engine;
using engine.world;
using Newtonsoft.Json.Linq;
using nogame.map;
using static engine.Logger;

namespace nogame;

/**
 * This is the game implementation main class.
 * It should
 * - setup te scenes in a way that a start set is or will be set
 * - setup all dependencies
 */
public class Main : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
    };


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        
        // TXWTODO: Looks a bit out of place, looks more like platform specific.
        I.Get<Boom.ISoundAPI>().SetupDone();
    }
}
