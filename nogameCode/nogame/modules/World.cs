using System;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.world;
using static engine.Logger;

namespace nogame.modules;

/**
 * What exactly is the purpose of this module?
 *
 * If this is supposed to contain the game "world" independently of the root scene,
 * we probably need to clean up and separate the triggerLoadWorld logic from the
 * creation of entities.
 */
public class World : AModule
{
    private engine.world.Loader _worldLoader;
    private engine.world.MetaGen _worldMetaGen;
    

    private void _triggerLoadWorld()
    {
        DefaultEcs.Entity eCamScene;
        if (!_engine.TryGetCameraEntity(out eCamScene) || !eCamScene.Has<Transform3ToWorld>())
        {
            return;
        }

        var cTransform3ToWorld = eCamScene.Get<Transform3ToWorld>(); 
        var vMe = cTransform3ToWorld.Matrix.Translation;

        bool shouldTryLoad = true;


        if (shouldTryLoad && vMe == Vector3.Zero)
        {
            shouldTryLoad = false;
        }
        if (shouldTryLoad && (vMe - new Vector3(99999f, 99999f, 99999f)).LengthSquared() < 1f)
        {
            shouldTryLoad = false;
        } 
        
        // TXWTODO: We don't precisely know when we have the first valid position 
        if (shouldTryLoad)
        {
            if (_worldLoader == null)
            {
                ErrorThrow("WorldLoader is null here?", m => new InvalidOperationException(m));
            }
            _worldLoader.WorldLoaderProvideFragments();
        }
    }
    
    
    private void _onLogicalFrame(object? sender, float dt)
    {
        _triggerLoadWorld();
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemoveModule(this);

        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        
        _worldMetaGen = I.Get<MetaGen>();
        _worldLoader = _worldMetaGen.Loader;
        if (null == _worldLoader)
        {
            ErrorThrow("_worldLoader is not supposed to be null here.", m => new InvalidOperationException(m));
            return;
        }
       
        /*
         * trigger generating the world at the starting point.
         */ 
        _triggerLoadWorld();
        
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}