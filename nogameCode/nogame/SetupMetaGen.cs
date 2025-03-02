
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine;
using engine.meta;
using engine.world;
using static engine.Logger;
using Loader = engine.casette.Loader;

namespace nogame;

public class SetupMetaGen
{
    private object _lo = new();
    
    private engine.Engine _engine;
    private engine.world.Loader _worldLoader;
    private engine.world.MetaGen _worldMetaGen;

    private bool _shallPreload = false;
    private Vector3 _v3Preload = default;

    
    public void PrepareMetaGen(engine.Engine engine0)
    {
        _engine = engine0;
        
        string keyScene = "abx";

        _worldMetaGen = I.Get<engine.world.MetaGen>();

        if (null == _worldMetaGen.EdRoot)
        {
            Error($"No edroot configured.");
        }

        _worldMetaGen.SetupComplete();
        _worldLoader = new engine.world.Loader();
        _worldMetaGen.SetLoader(_worldLoader);
        
        {
            var elevationCache = engine.elevation.Cache.Instance();
            var elevationBaseFactory = new terrain.ElevationBaseFactory();
            elevationCache.ElevationCacheRegisterElevationOperator(
                engine.elevation.Cache.LAYER_BASE + "/000002/fillGrid",
                elevationBaseFactory);
        }

        _worldMetaGen.Populate();

        Vector3 v3Preload;
        bool shallPreload;
        lock (_lo)
        {
            shallPreload = _shallPreload;
            v3Preload = _v3Preload;
        }

        if (shallPreload)
        {
            Preload(v3Preload);
        }
    }


    public void Preload(Vector3 pos)
    {
        Trace("Trying to preload...");

        bool preloadNow = false;
        lock (_lo)
        {
            if (_worldLoader != null)
            {
                preloadNow = true;
                _shallPreload = false;
            }
            else
            {
                preloadNow = false;
                _shallPreload = true;
                _v3Preload = pos;
            }
        }

        if (preloadNow)
        {
            var fixedPosViewer = new FixedPosViewer(_engine) { Position = pos };

            _worldLoader.AddViewer(fixedPosViewer);
            _worldLoader.WorldLoaderProvideFragments();
            _worldLoader.RemoveViewer(fixedPosViewer);
        }
    }
}