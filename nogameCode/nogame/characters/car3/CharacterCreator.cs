﻿using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin.loader;
using engine;
using engine.joyce;
using engine.physics;
using engine.world;
using engine.streets;
using nogame.cities;
using static engine.Logger;   


namespace nogame.characters.car3;


class CharacterCreator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    static public readonly string EntityName = "nogame.characters.car3";
    static private readonly float PhysicsMass = 500f;
    static private readonly float PhysicsRadius = 5f;
    static public BodyInertia PInertiaSphere = 
        new BepuPhysics.Collidables.Sphere(
            CharacterCreator.PhysicsRadius)
        .ComputeInertia(CharacterCreator.PhysicsMass);

    private static object _classLock = new();

    private static engine.audio.Sound[] _jCar3Sound;
    private static ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

    private static List<string> _primarycolors = new List<string>()
    {
        "#ff000000",
        "#ff0000ff",
        "#ff00ff00",
        // "#ff00ffff",
        "#ffff0000",
        "#ffff00ff",
        "#ffffff00"
        //"#ffffffff"
    };

    private static engine.audio.Sound _getCar3Sound(int i)
    {
        lock (_classLock)
        {
            if (_jCar3Sound == null)
            {
                _jCar3Sound = new engine.audio.Sound[4];
                for (int j = 0; j < 4; ++j)
                {
                    _jCar3Sound[j] = new engine.audio.Sound(
                        "car3noisemono.ogg", true, 0.5f, 0.7f + j*0.3f);
                }
            }

            return _jCar3Sound[i];
        }
    }
    
    
    private static bool _trace = false;

    private static string _carFileName(int carIdx)
    {
        return $"car{5 + carIdx}.obj";
    }
    

    static public StreetPoint? ChooseStreetPoint(builtin.tools.RandomSource rnd, Fragment worldFragment, ClusterDesc clusterDesc)
    {
        /*
         * Load all prerequisites
         */
        StreetPoint chosenStreetPoint = null;

        /*
         * First, generate the set of street points within this fragemnt.
         */
        var listInFragment = clusterDesc.GetStreetPointsInFragment(worldFragment.IdxFragment);
        var nStreetPoints = listInFragment.Count(); 
        if (nStreetPoints == 0)
        {
            return null;
        }

        var idxPoint = rnd.GetInt(nStreetPoints - 1);
        return listInFragment[idxPoint];
    }


    public static async Task<DefaultEcs.Entity> GenerateRandomCharacter(
        builtin.tools.RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        StreetPoint chosenStreetPoint,
        int seed = 0)
    {
        int carIdx = (int)(rnd.GetFloat() * 4f);
        int colorIdx = (int)(rnd.GetFloat() * (float)_primarycolors.Count);
        var modelProperties = new ModelProperties()
        {
            ["primarycolor"] = _primarycolors[colorIdx],
        };
        string strModel = _carFileName(carIdx);
        float propMaxDistance = (float)engine.Props.Get("nogame.characters.car3.maxDistance", 800f);
        
        engine.behave.IBehavior iBehavior = 
            new car3.Behavior()
            {
                Navigator = new StreetNavigationController()
                {
                    ClusterDesc = clusterDesc,
                    StartPoint = chosenStreetPoint,
                    Seed = seed,
                    Speed = (30f + rnd.GetFloat() * 20f + (float)carIdx * 20f) / 3.6f    
                }
            };
        var sound = _getCar3Sound(carIdx);
        ModelCacheParams mcp = new()
        {
            Url = strModel,
            Properties = new(modelProperties),
            Params = new()
            {
                GeomFlags = 0 | InstantiateModelParams.CENTER_X
                              | InstantiateModelParams.CENTER_Z
                              | InstantiateModelParams.ROTATE_Y180
                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                              | InstantiateModelParams.BUILD_PHYSICS
                              | InstantiateModelParams.PHYSICS_DETECTABLE
                              | InstantiateModelParams.PHYSICS_TANGIBLE
                              | InstantiateModelParams.PHYSICS_CALLBACKS
                ,
                MaxDistance = propMaxDistance,
                
                CollisionLayers = 0x0002,
            }
        };
        
        Model model = await I.Get<ModelCache>().LoadModel(mcp);

        return GenerateCharacter(
            clusterDesc, worldFragment, chosenStreetPoint, 
            model, mcp, iBehavior, sound);
    }

    
    public static void SetupCharacterMT(
        DefaultEcs.Entity eTarget,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        StreetPoint chosenStreetPoint,
        Model model,
        ModelCacheParams mcp,
        engine.behave.IBehavior? iBehavior,
        engine.audio.Sound? sound)
    {
        var wf = worldFragment;
        int fragmentId = worldFragment.NumericalId;

        eTarget.Set(new engine.world.components.Owner(fragmentId));

        /*
         * We already setup the FromModel in case we utilize one of the characters as
         * subject of a Quest.
         */
        eTarget.Set(new engine.joyce.components.FromModel() { Model = model, ModelCacheParams = mcp });

        if (iBehavior != null)
        {
            eTarget.Set(new engine.behave.components.Behavior()
            {
                Provider = iBehavior,
                MaxDistance = (short) mcp.Params.MaxDistance
            });
        }

        if (sound != null)
        {
            eTarget.Set(new engine.audio.components.MovingSound(
                sound, mcp.Params.MaxDistance));
        }

        /*
         * We need to set a preliminary Transform3World component. Invisible, but inside the fragment.
         * That way, the character will not be cleaned up immediately.
         */
        eTarget.Set(new engine.joyce.components.Transform3ToWorld(0, 0,
            Matrix4x4.CreateTranslation(worldFragment.Position)));

        I.Get<ModelCache>().BuildPerInstance(eTarget, model, mcp);
    }
    
    
    public static DefaultEcs.Entity GenerateCharacter(
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        StreetPoint chosenStreetPoint,
        Model model,
        ModelCacheParams mcp,
        engine.behave.IBehavior? iBehavior,
        engine.audio.Sound? sound)
    {
        TaskCompletionSource<DefaultEcs.Entity> taskCompletionSource = new();
        Task<DefaultEcs.Entity> taskResult = taskCompletionSource.Task;

        var wf = worldFragment;

        int fragmentId = worldFragment.NumericalId;

        wf.Engine.QueueEntitySetupAction(EntityName, eTarget =>
        {
            SetupCharacterMT(eTarget,
                clusterDesc, worldFragment, chosenStreetPoint,
                model, mcp, iBehavior, sound);
            taskCompletionSource.SetResult(eTarget);
        });
        
        return taskResult.Result;
    }

}
