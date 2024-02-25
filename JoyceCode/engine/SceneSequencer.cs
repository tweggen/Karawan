using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text.Json;

using static engine.Logger;

namespace engine;

public class SceneSequencer : IDisposable
{
    private readonly object _lo = new();
    private readonly Engine _engine;
    
    private IScene _sceneNewMain = null;
    private string _nameNewMainScene = "";
    
    private IScene _mainScene = null;
    private string _nameMainScene = "";

    private readonly SortedDictionary<string, Func<IScene>> _dictSceneFactories = new();
    private readonly SortedDictionary<float, IScene> _dictScenes = new();

    public IScene MainScene
    {
        get { lock(_lo) { return _mainScene;} }
    }

    public IList<string> GetAvailableScenes()
    {
        lock (_lo)
        {
            return new List<string>(_dictSceneFactories.Keys);
        }
    }


    /**
     * If there is a new scene to set up, deactivate the old scene,
     * activate the new one.
     *
     * THen, call all scenes that currently are active.
     */
    private void _loadNewMainScene()
    {
        IScene scene = null;
        IScene oldScene = null;
        lock (_lo)
        {
            scene = _sceneNewMain;
            _sceneNewMain = null;
            if (null == scene)
            {
                _nameNewMainScene = "";
                return;
            }
            oldScene = _mainScene;
            _mainScene = null;
            _nameMainScene =_nameNewMainScene;
            _nameNewMainScene = "";
            _nameNewSceneRequest = "";
        }
        if (oldScene != null)
        {
            oldScene.ModuleDeactivate();
        }
        
        // TXWTODO: Wait for old scene to be done? No? Fadeouts??
     
        lock (_lo)
        {
            _mainScene = scene;
        }
        if (scene != null)
        {
            scene.ModuleActivate(_engine);
        }

    }


    private void _callAllSceneLogicalFrames(float dt)
    {
        SortedDictionary<float, IScene> dictScenes;
        lock (_lo)
        {
            dictScenes = new SortedDictionary<float, IScene>(_dictScenes);
        }
        foreach (KeyValuePair<float, IScene> kvp in dictScenes)
        {
            kvp.Value.SceneOnLogicalFrame(dt);
        }
    }


    private void OnOnLogicalFrame(object sender, float dt)
    {
        _loadNewMainScene();
        _callAllSceneLogicalFrames(dt);
    }
    
    /**
     * Remove an active scene. Several scenes may be active at once.
     */
    public void RemoveScene(in IScene scene)
    {
        foreach( KeyValuePair<float, IScene> kvp in _dictScenes )
        {
            if( kvp.Value == scene )
            {
                _dictScenes.Remove(kvp.Key);
                return;
            }
        }
    }


    private void SetMainScene(in IScene scene, in string name)
    {
        lock(_lo)
        {
            if (_nameMainScene == name)
            {
                ErrorThrow<ArgumentException>($"Trying to activate scene that already is activated {_nameMainScene}.");
            }
            _sceneNewMain = scene;
            _nameNewMainScene = name;
        }
    }


    /**
     * Workaround for a race condition before loading main scene
     */
    private bool _checkedMainScene(string name)
    {
        if ("logos"!=name && null == engine.world.MetaGen.Instance().Loader)
        {
            return false;
        }

        Func<IScene> factoryFunction = null;
        lock(_lo)
        {
            factoryFunction = _dictSceneFactories[name];
        }
        IScene scene = factoryFunction();
        SetMainScene(scene, name);
        return true;
    }

    private string _nameNewSceneRequest = "";
    public void SetMainScene(string name)
    {
        lock (_lo)
        {
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace(true);
            Trace($"Called SetMainScene at {t.ToString()}");

            if (_nameNewSceneRequest == name)
            {
                Error($"Trying to activate scene that already is activated.");
                return;
            }
            _nameNewSceneRequest = name;
        }
        I.Get<Timeline>().RunAt("", TimeSpan.Zero, () =>
        {
             return _checkedMainScene(name);
        });
    }
    
    
    /**
     * Add an active scene. Several scenes may be active at once.
     */
    public void AddScene(float zOrder, in IScene scene)
    {
        _dictScenes.Add(zOrder, scene);
    }

    
    public void AddSceneFactory(in string name, in Func<IScene> factoryFunction)
    {
        lock(_lo)
        {
            _dictSceneFactories.Add(name, factoryFunction);
        }
    }


    public void AddFrom(JsonElement jeScenes)
    {
        try
        {
            foreach (var pair in jeScenes.EnumerateObject())
            {
                var jeClassDesc = pair.Value;
                AddSceneFactory(pair.Name, () =>
                {
                    var className = jeClassDesc.GetProperty("className").GetString();
                    if (String.IsNullOrWhiteSpace(className))
                    {
                        Warning($"Encountered null classname for {pair}.");
                    }
                    try
                    {
                        IScene scene = engine.rom.Loader.LoadClass("nogame.dll", className) as IScene;
                        return scene;
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to load scene {pair.Name}: {e}");
                    }

                    return null;
                });
            }
            // TXWTODO: Also get start scene here.
        }
        catch (Exception e)
        {
            Warning($"No scenes found to add or error in declaration.");
        }
    }
    


    public void Dispose()
    {
        _engine.OnLogicalFrame -= OnOnLogicalFrame;
    }
    
    
    public SceneSequencer(Engine engine)
    {
        _engine = engine;
        _engine.OnLogicalFrame += OnOnLogicalFrame;
    }
}