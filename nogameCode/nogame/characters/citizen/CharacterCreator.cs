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
        string[] strModels =
        {
            "Studio Ochi Spring Boy_ANIM.fbx",
            "Studio Ochi Spring Man B_ANIM.fbx",
            "Studio Ochi Spring Woman C_ANIM.fbx"
        };

        string strModel;
        string strAnimation = null;
        float which = rnd.GetFloat();
        float speed;
        if (which < 0.2f)
        {
            strModel = strModels[0];
            speed = (6f + rnd.GetFloat() * 2f) / 3.6f;
            strAnimation = "Metarig Boy|Run Mid";
        }
        else if (which < 0.6f)
        {
            strModel = strModels[1];
            speed = (6.5f + rnd.GetFloat() * 2f) / 3.6f;
            strAnimation = "Metarig Man B|Run Mid";
        }
        else
        {
            strModel = strModels[2];
            speed = (6f + rnd.GetFloat() * 2f) / 3.6f;
            strAnimation = "Metarig Woman C|Run Mid";
        }

        float propMaxDistance = (float)engine.Props.Get("nogame.characters.citizen.maxDistance", 100f);

        var snc = new StreetNavigationController()
        {
            Height = 2.5f,
            ClusterDesc = clusterDesc,
            StartPoint = chosenStreetPoint,
            Seed = seed,
            Speed = speed
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
                GeomFlags = 0
                            //| InstantiateModelParams.CENTER_X
                            //| InstantiateModelParams.ROTATE_X90
                            //| InstantiateModelParams.ROTATE_Y180
                ,
                MaxDistance = propMaxDistance,
                
                // CollisionLayers = 0x0002,
            }
        };
        
        Model model = await I.Get<ModelCache>().LoadModel(mcp);

        return _generateCharacter(
            clusterDesc, worldFragment, chosenStreetPoint, 
            model, mcp, strAnimation, iBehavior, null);
    }

    
    private static void _setupCharacterMT(
        DefaultEcs.Entity eTarget,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        StreetPoint chosenStreetPoint,
        Model model,
        ModelCacheParams mcp,
        string strAnimation,
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
            // I.Get<ModelCache>().BuildPerInstancePhysics(eTarget, modelBuilder, model, mcp);
            eAnimations = modelBuilder.GetAnimationsEntity();
        }

        if (default != eAnimations)
        {
            var mapAnimations = model.MapAnimations;
            if (mapAnimations != null && mapAnimations.Count > 0)
            {
                var animation = mapAnimations[strAnimation];
                eAnimations.Set(new AnimationState
                {
                    ModelAnimation = animation,
                    ModelAnimationFrame = 0
                });
                // Trace($"Setting up animation {animation.Name}");
            }
        }

 
    }
    
    
    private static DefaultEcs.Entity _generateCharacter(
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        StreetPoint chosenStreetPoint,
        Model model,
        ModelCacheParams mcp,
        string strAnimation,
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
            _setupCharacterMT(eTarget,
                clusterDesc, worldFragment, chosenStreetPoint,
                model, mcp, strAnimation, iBehavior, sound);
            taskCompletionSource.SetResult(eTarget);
        });
        
        return taskResult.Result;
    }


}