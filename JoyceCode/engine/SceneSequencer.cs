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
    private IScene _mainScene = null;

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
                return;
            }
            oldScene = _mainScene;
            _mainScene = null;
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


    public void SetMainScene(in IScene scene)
    {
        lock(_lo)
        {
            _sceneNewMain = scene;
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
        SetMainScene(scene);
        return true;
    }
    
    
    public void SetMainScene(string name)
    {
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


    public void AddFrom(JsonElement je)
    {
        try
        {
            var jeScenes = je.GetProperty("scenes");
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
                        IScene scene = engine.Engine.LoadClass("nogame.dll", className) as IScene;
                        return scene;
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to load scene {pair.Name}: {e}");
                    }

                    return null;
                });
            }

            var jeStartScene = je.GetProperty("startScene");
            string strStartSceneName = jeStartScene.GetProperty("name").GetString();
            SetMainScene(strStartSceneName);
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