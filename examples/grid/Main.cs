using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;

namespace grid;

/// <summary>
/// Main module for the grid example.
/// Sets up a minimal scene with a cube, camera, and directional light.
/// </summary>
public class Main : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<engine.Resources>(),
    };

    private void _setupScreenComposition()
    {
        // Create a renderbuffer for the 3D scene
        I.Get<ObjectRegistry<Renderbuffer>>().RegisterFactory(
            "main_3d",
            name => new Renderbuffer(name, 1280, 720));

        // Add the 3D layer to the screen composer
        // Note: We don't use ScreenComposer in this minimal example,
        // so we just need the renderbuffer for the camera
    }

    protected override void OnModuleActivate()
    {
        _setupScreenComposition();

        // Activate the scene
        var scene = new Scene();
        _engine.AddModule(scene); 
        
        // Sound API setup done (even if we don't use sound)
        I.Get<Boom.ISoundAPI>()?.SetupDone();
        
        I.Get<SceneSequencer>().Load();
        I.Get<SceneSequencer>().Run();
    }
}
