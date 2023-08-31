
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine.meta;
using static engine.Logger;

namespace nogame;

public class SetupMetaGen
{
    private engine.Engine _engine;
    private engine.world.Loader _worldLoader;
    private engine.world.MetaGen _worldMetaGen;

    
    public void PrepareMetaGen(engine.Engine engine0)
    {
        _engine = engine0;
        
        string keyScene = "abx";

        _worldMetaGen = engine.world.MetaGen.Instance();

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
                            Mode = ExecDesc.ExecMode.Task,
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
                                            Implementation = "nogame.cities.GeneratePolytopeOperator.InstantiateFragmentOperator"
                                        },
                                        new () 
                                        {
                                            ConfigCondition = "world.CreateCubeCharacters",
                                            Implementation = "nogame.characters.cubes.GenerateCharacterOperator.InstantiateFragmentOperator"
                                        },
                                        new () 
                                        {
                                            ConfigCondition = "world.CreateCar3Characters",
                                            Implementation = "nogame.characters.car3.GenerateCharacterOperator.InstantiateFragmentOperator"
                                        },
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

        _worldLoader = new engine.world.Loader(_engine, _worldMetaGen);
        {
            var elevationCache = engine.elevation.Cache.Instance();
            var elevationBaseFactory = new terrain.ElevationBaseFactory();
            elevationCache.ElevationCacheRegisterElevationOperator(
                engine.elevation.Cache.LAYER_BASE + "/000002/fillGrid",
                elevationBaseFactory);
        }
    }


    public void Preload(Vector3 pos)
    {
        _worldLoader.WorldLoaderProvideFragments(pos);
    }
}