using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.loader;
using builtin.tools;
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
    
    
    static public void ChooseQuarterDelimPointPos(
        builtin.tools.RandomSource rnd, Fragment worldFragment, ClusterDesc clusterDesc,
        out Quarter quarter, out QuarterDelim delim, out float relativePos)
    {
        quarter = null;
        delim = null;
        relativePos = 0f;
        
        var quarterStore = clusterDesc.QuarterStore();
        if (null == quarterStore)
        {
            return;
        }

        var quartersList = quarterStore.GetQuarters();
        if (null == quartersList)
        {
            return;
        }

        int nQuarters = quartersList.Count;
        if (nQuarters == 0)
        {
            return;
        }

        int idxQuarter = (int)(rnd.GetFloat() * nQuarters);
        quarter = quartersList[idxQuarter];
        if (null == quarter)
        {
            return;
        }

        var quarterDelims = quarter.GetDelims();
        if (null == quarterDelims || quarterDelims.Count <= 1)
        {
            quarter = null;
            return;
        }

        int nDelims = quarterDelims.Count;
        int idxDelim = (int)(rnd.GetFloat() * nDelims);
        delim = quarterDelims[idxDelim];
        relativePos = rnd.GetFloat();
        return;
    }


    private static engine.behave.IBehavior _createDefaultBehavior(
        RandomSource rnd,
        ClusterDesc clusterDesc, 
        Quarter quarter, QuarterDelim delim, float relativePosition)
    {
        List<builtin.tools.SegmentEnd> listSegments = new();

        var delims = quarter.GetDelims();
        int l = delims.Count;

        int startIndex = 0;
        for (int i = 0; i < l; ++i)
        {
            var dlThis = delims[i];
            var dlNext = delims[(i + 1) % l];

            if (delim == dlThis)
            {
                startIndex = i;
            }
            
            var v3This = new Vector3(dlThis.StartPoint.X, clusterDesc.AverageHeight + 2.5f, dlThis.StartPoint.Y );
            var v3Next = new Vector3(dlNext.StartPoint.X, clusterDesc.AverageHeight + 2.5f, dlNext.StartPoint.Y);
            v3This += clusterDesc.Pos;
            v3Next += clusterDesc.Pos;
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            
            listSegments.Add(
                new()
                {
                    Position = v3This,
                    Up = vu3Up,
                    Right = Vector3.Cross(vu3Forward, vu3Up)
                });
        }


        builtin.tools.SegmentNavigator segnav = new ()
        {
            ListSegments = listSegments,
            StartIndex = startIndex,
            StartRelative = rnd.GetFloat(),
            LoopSegments = true,
            Speed = 4f
        };

        return new SimpleNavigationBehavior()
        {
            Navigator = segnav
        };
    }
    

    public static async Task<DefaultEcs.Entity> GenerateRandomCharacter(
        builtin.tools.RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        Quarter quarter,
        QuarterDelim delim,
        float relativePos,
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

        engine.behave.IBehavior iBehavior = _createDefaultBehavior(rnd, clusterDesc, quarter, delim, relativePos);
         
        // var sound = _getCitizenSound(carIdx);
        ModelCacheParams mcp = new()
        {
            Url = strModel,
            Properties = new(modelProperties),
            Params = new()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.ROTATE_X90
                            | InstantiateModelParams.ROTATE_Y180
                ,
                MaxDistance = propMaxDistance,
                
                // CollisionLayers = 0x0002,
            }
        };
        
        Model model = await I.Get<ModelCache>().LoadModel(mcp);

        return _generateCharacter(
            clusterDesc, worldFragment,  
            model, mcp, strAnimation, iBehavior, null);
    }

    
    private static void _setupCharacterMT(
        DefaultEcs.Entity eTarget,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
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
                clusterDesc, worldFragment, 
                model, mcp, strAnimation, iBehavior, sound);
            taskCompletionSource.SetResult(eTarget);
        });
        
        return taskResult.Result;
    }


}