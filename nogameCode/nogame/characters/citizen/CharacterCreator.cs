using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.loader;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.streets;
using engine.world;
using nogame.cities;
using static engine.Logger;

namespace nogame.characters.citizen;

public class CharacterCreator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    public static readonly string EntityName = "nogame.characters.citizen";
    private static readonly float PhysicsMass = 60f;
    private static readonly float PhysicsRadius = 1f;
    public static BodyInertia PInertiaSphere = 
        new BepuPhysics.Collidables.Sphere(
            CharacterCreator.PhysicsRadius)
        .ComputeInertia(CharacterCreator.PhysicsMass);

    private static object _classLock = new();

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

    private static bool _trace = false;
    
    
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
        var modelProperties = new ModelProperties()
        {
        };
        string strModel = "Studio Ochi Spring Boy_ANIM.fbx";
        float propMaxDistance = (float)engine.Props.Get("nogame.characters.citizen.maxDistance", 100f);

        var snc = new StreetNavigationController()
        {
            Height = 2.5f,
            ClusterDesc = clusterDesc,
            StartPoint = chosenStreetPoint,
            Seed = seed,
            Speed = (6f + rnd.GetFloat() * 2f) / 3.6f
        };
        snc.CreateStrokeProperties = snc.ComputePedestrianProperties;
        
        engine.behave.IBehavior iBehavior = 
            new citizen.Behavior()
            {
                Navigator = snc
            };
         
        // var sound = _getCitizenSound(carIdx);
        ModelCacheParams mcp = new()
        {
            Url = strModel,
            Properties = new(modelProperties),
            Params = new()
            {
                GeomFlags = 0 | InstantiateModelParams.CENTER_X
                              // | InstantiateModelParams.CENTER_Z
                              | InstantiateModelParams.ROTATE_X90
                              | InstantiateModelParams.ROTATE_Y180
                              // | InstantiateModelParams.BUILD_PHYSICS
                              // | InstantiateModelParams.PHYSICS_DETECTABLE
                              // | InstantiateModelParams.PHYSICS_TANGIBLE
                              // | InstantiateModelParams.PHYSICS_CALLBACKS
                ,
                MaxDistance = propMaxDistance,
                
                // CollisionLayers = 0x0002,
            }
        };
        
        Model model = await I.Get<ModelCache>().LoadModel(mcp);

        return GenerateCharacter(
            clusterDesc, worldFragment, chosenStreetPoint, 
            model, mcp, iBehavior, null);
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

        DefaultEcs.Entity eAnimations;
        
        {
            builtin.tools.ModelBuilder modelBuilder = new(I.Get<Engine>(), model, mcp.Params);
            modelBuilder.BuildEntity(eTarget);
            I.Get<ModelCache>().BuildPerInstancePhysics(eTarget, modelBuilder, model, mcp);
            eAnimations = modelBuilder.GetAnimationsEntity();
        }

        if (default != eAnimations)
        {
            var mapAnimations = model.MapAnimations;
            if (mapAnimations != null && mapAnimations.Count > 0)
            {
                var animation = mapAnimations["Metarig Boy|Run Mid"];
                eAnimations.Set(new AnimationState
                {
                    ModelAnimation = animation,
                    ModelAnimationFrame = 0
                });
                // Trace($"Setting up animation {animation.Name}");
            }
        }

 
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

        string name = mcp.Params?.Name;
        if (String.IsNullOrWhiteSpace(name))
        {
            name = EntityName;
        }

        wf.Engine.QueueEntitySetupAction(name, eTarget =>
        {
            SetupCharacterMT(eTarget,
                clusterDesc, worldFragment, chosenStreetPoint,
                model, mcp, iBehavior, sound);
            taskCompletionSource.SetResult(eTarget);
        });
        
        return taskResult.Result;
    }


}