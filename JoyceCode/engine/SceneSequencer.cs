using System;
using System.Collections.Generic;

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
            oldScene.SceneDeactivate();
        }
        
        // TXWTODO: Wait for old scene to be done? No? Fadeouts??
     
        lock (_lo)
        {
            _mainScene = scene;
        }
        if (scene != null)
        {
            scene.SceneActivate(_engine);
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

    
    public void SetMainScene(in string name)
    {
        Func<IScene> factoryFunction = null;
        lock(_lo)
        {
            factoryFunction = _dictSceneFactories[name];
        }
        IScene scene = factoryFunction();
        SetMainScene(scene);
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