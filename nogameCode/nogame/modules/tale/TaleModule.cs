using System;
using System.Collections.Generic;
using System.IO;
using engine;
using engine.tale;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// Module that bootstraps the TALE narrative system.
/// Loads the storylet library and creates the TaleManager singleton.
/// </summary>
public class TaleModule : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<modules.daynite.Controller>()
    };


    protected override void OnModuleActivate()
    {
        try
        {
            // Load storylet library from models/tale/
            string resourcePath = GlobalSettings.Get("Engine.ResourcePath") ?? "./models/";
            string talePath = Path.Combine(resourcePath, "tale");

            if (!Directory.Exists(talePath))
            {
                Trace($"TALE: No tale directory at {talePath}, module inactive.");
                return;
            }

            var library = new StoryletLibrary();
            library.LoadFromDirectory(talePath);
            Trace($"TALE: Loaded {library.GetCandidates("worker").Count} storylets.");

            // Create and register TaleManager
            var taleManager = new TaleManager();
            taleManager.Initialize(library, null);

            I.Register<TaleManager>(() => taleManager);
            Trace("TALE: TaleManager registered.");
        }
        catch (Exception e)
        {
            Warning($"TALE: Failed to initialize: {e}");
        }
    }
}
