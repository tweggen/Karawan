
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine;
using engine.meta;
using engine.world;
using static engine.Logger;

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

        engine.meta.ExecDesc edRoot = new()
        {
            Mode = ExecDesc.ExecMode.Sequence,
            Comment = "We need a top level sequence of executing things for the fragments."
                + "This is not much, but first we need to get the terrain height done.",
            Children = new List<ExecDesc>()
            {
                new()
                {
                    Implementation = "engine.world.CreateTerrainOperator.InstantiateFragmentOperator",
                },
                new()
                {
                    Mode = ExecDesc.ExecMode.Parallel,
                    Comment = "Now, there's no particular order to prepare the remaining things.",
                    Children = new()
                    {
                        new()
                        {
                            Implementation = "engine.world.CreateTerrainMeshOperator.InstantiateFragmentOperator"  
                        },
                        new ()
                        {
                            Implementation = "nogame.terrain.PlaceDebrisOperator.InstantiateFragmentOperator"
                        },
                        new()
                        {
                            Mode = ExecDesc.ExecMode.ApplyParallel,
                            Comment = "This includes all cluster operators.",
                            Selector = "clusterDescList",
                            Target = "clusterDesc",
                            Children = new()
                            {
                                new()
                                {
                                    Mode = ExecDesc.ExecMode.Parallel,
                                    Children = new()
                                    {
                                        new ()
                                        {
                                            Implementation = "engine.streets.GenerateClusterStreetsOperator.InstantiateFragmentOperator",
                                        },
                                        new ()
                                        {
                                            ConfigCondition = "nogame.CreateHouses",
                                            Implementation = "nogame.cities.GenerateHousesOperator.InstantiateFragmentOperator"
                                        },
                                        new ()
                                        {
                                            ConfigCondition = "world.CreateClusterQuarters",
                                            Implementation = "engine.streets.GenerateClusterQuartersOperator.InstantiateFragmentOperator",                                            
                                        },
                                        new ()
                                        {
                                            ConfigCondition = "world.CreateStreetAnnotations",
                                            Implementation = "engine.streets.GenerateClusterStreetAnnotationsOperator.InstantiateFragmentOperator",
                                        },
                                        new ()
                                        {
                                            ConfigCondition = "nogame.CreateTrees",
                                            Implementation = "nogame.cities.GenerateTreesOperator.InstantiateFragmentOperator"
                                        },
                                        new ()
                                        {
                                            ConfigCondition = "nogame.CreatePolytopes",
                                            Implementation = "nogame.cities.GeneratePolytopeOperator.InstantiateFragmentOperator"
                                        },
                                        new () 
                                        {
                                            ConfigCondition = "world.CreateCubeCharacters",
                                            Implementation = "nogame.characters.cubes.GenerateCharacterOperator.InstantiateFragmentOperator"
                                        },
                                        #if false
                                        new () 
                                        {
                                            ConfigCondition = "world.CreateCar3Characters",
                                            Implementation = "nogame.characters.car3.GenerateCharacterOperator.InstantiateFragmentOperator"
                                        },
                                        #endif
                                        new () 
                                        {
                                            ConfigCondition = "world.CreateTramCharacters",
                                            Implementation = "nogame.characters.tram.GenerateCharacterOperator.InstantiateFragmentOperator"
                                        },
                                    }
                                }
                            }
                        },
                    }
                }
            }
        };
        _worldMetaGen.EdRoot = edRoot;
        {
            JsonSerializerOptions options = new()
            {
                // ReferenceHandler = ReferenceHandler.Preserve,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            string jsonEdRoot = JsonSerializer.Serialize(edRoot, options);
            Trace("MetaGen Configuration: ");
            Trace(jsonEdRoot);
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