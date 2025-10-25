using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.loader;
using builtin.tools;
using engine;
using engine.geom;
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
    /**
     * If this is one, all animations of one character are using the same frame at any given time.
     * Setting this to two limits this to two different animation phases. That way on mobile platforms
     * that do not upload animation tables to gpu but require distinct draw calls, draw calls are saved.
     */
    public static uint NDrawCallsPerCharacterBatch { get; set; } = 2;
    
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

            float h = clusterDesc.AverageHeight + engine.world.MetaGen.ClusterStreetHeight +
                      engine.world.MetaGen.QuaterSidewalkOffset;
            var v3This = new Vector3(dlThis.StartPoint.X, h, dlThis.StartPoint.Y );
            var v3Next = new Vector3(dlNext.StartPoint.X, h, dlNext.StartPoint.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);
            v3This += -1.5f * vu3Right;
            
            listSegments.Add(
                new()
                {
                    Position = v3This + clusterDesc.Pos,
                    Up = vu3Up,
                    Right = vu3Right
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

        return new nogame.characters.citizen.Behavior()
        {
            Navigator = segnav
        };
    }
    

    public static async Task<Action<DefaultEcs.Entity>> GenerateRandomCharacter(
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
        #if false
        string[] strModels =
        {
            "Studio Ochi Spring Boy_ANIM.fbx",
            "Studio Ochi Spring Man B_ANIM.fbx",
            "Studio Ochi Spring Woman C_ANIM.fbx"
        };
        #else
        string[] strModels =
        {
            "man_business_Rig.fbx",
            "man_casual_Rig.fbx",
            "man_coat_winter_Rig.fbx",
            "man_police_Rig.fbx",
            "man_scientist_Rig.fbx",
            "woman_actionhero_Rig.fbx",
            "woman_carpenter_Rig.fbx",
            "woman_doctor_Rig.fbx",
            "woman_large_Rig.fbx",
            "woman_mechanic_Rig.fbx",
            "woman_police_Rig.fbx",
            "woman_race_car_driver_Rig.fbx"
        };
        #endif

        string strModel;
        string strAnimation = null;
        float speed;
        
        #if true
 
        speed = (5f + rnd.GetFloat() * 4f) / 3.6f;
        int idxModel = (int)(rnd.GetFloat() * strModels.Length);
        strModel = strModels[idxModel];
        bool isMale = strModel.StartsWith("man");
        
        if (speed > (7f / 3.6f))
        {
            strAnimation = "Run_InPlace";
        }
        else
        {
            if (isMale)
            {
                strAnimation = "Walk_Male";
            } else 
            {
                strAnimation = "Walk_InPlace_Female";
            }
        }
        #endif
        #if false
        float which = rnd.GetFloat();
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
        #endif

        float propMaxDistance = (float)engine.Props.Get("nogame.characters.citizen.maxDistance", 100f);

        engine.behave.IBehavior iBehavior = _createDefaultBehavior(rnd, clusterDesc, quarter, delim, relativePos);
         
        // var sound = _getCitizenSound(carIdx);
        ModelCacheParams mcp = new()
        {
            Url = strModel,
            Properties = new(modelProperties) { Properties = new()
                {
                    //{ "Scale", "0,01" }
                    //{"Axis", "XzY"}
                    { "Axis", "XYZ" },
                    //{ "AnimAxis", "XZy" }
                    { "AnimationUrls", "Run_InPlace.fbx;Walk_Male.fbx;Walk_InPlace_Female.fbx" }
                } 
            },
            Params = new()
            {
                GeomFlags = 0
                            #if false
                            | InstantiateModelParams.CENTER_X
                            // | InstantiateModelParams.ROTATE_X180
                            | InstantiateModelParams.ROTATE_Z180
                            #endif
                            #if true
                            | InstantiateModelParams.ROTATE_Y180
                            #endif
                            | InstantiateModelParams.BUILD_PHYSICS
                            | InstantiateModelParams.PHYSICS_TANGIBLE
                            | InstantiateModelParams.PHYSICS_DETECTABLE
                            | InstantiateModelParams.PHYSICS_CALLBACKS
                ,
                MaxVisibilityDistance = propMaxDistance,
                MaxBehaviorDistance = propMaxDistance,
                MaxAudioDistance = propMaxDistance,
                MaxPhysicsDistance = 4f,
                
                PhysicsAABB = new AABB(new Vector3(-0.30f, 0f, -0.15f), new Vector3(0.3f, 1.85f, 0.15f)),
                CollisionLayers = 0x0002,
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
        string? strAnimation,
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
                MaxDistance = (short) mcp.Params.MaxBehaviorDistance
            });
        }

        if (sound != null)
        {
            eTarget.Set(new engine.audio.components.MovingSound(
                sound, mcp.Params.MaxAudioDistance));
        }

        /*
         * We need to set a preliminary Transform3World component. Invisible, but inside the fragment.
         * That way, the character will not be cleaned up immediately.
         */
        eTarget.Set(new engine.joyce.components.Transform3ToWorld(0, 0,
            Matrix4x4.CreateTranslation(worldFragment.Position)));

        DefaultEcs.Entity eAnimations;
        
        
        #if true
        {
            builtin.tools.ModelBuilder modelBuilder = new(I.Get<Engine>(), model, mcp.Params);
            modelBuilder.BuildEntity(eTarget);
            I.Get<ModelCache>().BuildPerInstancePhysics(eTarget, modelBuilder, model, mcp);
            eAnimations = modelBuilder.GetAnimationsEntity();
        }
        #else
        I.Get<ModelCache>().BuildPerInstance(eTarget, model, mcp);
        #endif

        /*
         * If we created physics for this one, take care to minimize
         * the distance for physics support.
         */
        if (eTarget.Has<engine.physics.components.Body>())
        {
            ref var cBody = ref eTarget.Get<engine.physics.components.Body>();
            if (cBody.PhysicsObject != null)
            {
                cBody.PhysicsObject.MaxDistance = 10f;
            }
        }
        
        if (strAnimation != null && default != eAnimations)
        {
            var mapAnimations = model.MapAnimations;
            if (mapAnimations != null && mapAnimations.Count > 0)
            {
                if (!mapAnimations.ContainsKey(strAnimation))
                {
                    int a = 1;
                }
                var animation = mapAnimations[strAnimation];
                eAnimations.Set(new GPUAnimationState
                {
                    AnimationState = new()
                    {
                        ModelAnimation = animation,
                        ModelAnimationFrame = (ushort)((NDrawCallsPerCharacterBatch>0)?
                            (I.Get<Engine>().FrameNumber % (animation.NFrames/NDrawCallsPerCharacterBatch))
                            :0)
                    }
                });
                // Trace($"Setting up animation {animation.Name}");
            }
        }

 
    }
    
    
    private static Action<DefaultEcs.Entity> _generateCharacter(
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        Model model,
        ModelCacheParams mcp,
        string? strAnimation,
        engine.behave.IBehavior? iBehavior,
        engine.audio.Sound? sound) 
    {
        return eTarget =>
        {
            _setupCharacterMT(eTarget,
                clusterDesc, worldFragment, 
                model, mcp, strAnimation, iBehavior, sound);
        };
    }


}