using System.Numerics;
using builtin.tools;
using engine.geom;
using engine.physics;

namespace nogame.npcs.niceday;

using engine.joyce;

public static class CharacterModelDescriptionFactory
{
    private static readonly string [] _arrModels = 
    {
        "man_coat_winter_Rig.fbx",
    };

    public static nogame.characters.CharacterModelDescription CreateNPC(RandomSource rnd)
    {
        string strModel;
        int idxModel = (int)(rnd.GetFloat() * _arrModels.Length);
        strModel = _arrModels[idxModel];        
        bool isMale = strModel.StartsWith("man");
        float propMaxDistance = (float)engine.Props.Get("nogame.npcs.maxDistance", 200f);
        
        return new()
        {
            IdleAnimName = "Idle_HardDay",
            AnimationUrls = $"Idle_HardDay.fbx;Run_InPlace.fbx;{(isMale?"Walk_Male.fbx":"Walk_InPlace_Female.fbx")};Death_FallForwards.fbx",
            ModelBaseBone = "Root_M",
            ModelUrl = strModel,
            Scale = "1",
            InstantiateModelParams = new()
            {               
                GeomFlags = 0
                            | InstantiateModelParams.BUILD_PHYSICS
                            /*
                             * We better have the cititen non-tangible to not stop the
                             * car or anybody running through them. They may, however, react.
                             */ 
                            // | InstantiateModelParams.PHYSICS_TANGIBLE
                            | InstantiateModelParams.PHYSICS_DETECTABLE
                            | InstantiateModelParams.PHYSICS_CALLBACKS
                ,
                MaxVisibilityDistance = propMaxDistance,
                MaxBehaviorDistance = propMaxDistance,
                MaxAudioDistance = propMaxDistance,
                MaxPhysicsDistance = 4f,
            
                PhysicsAABB = new AABB(new Vector3(-0.30f, 0f, -0.15f), new Vector3(0.3f, 1.85f, 0.15f)),
                SolidLayerMask = CollisionProperties.Layers.NpcCharacter,
                SensitiveLayerMask = CollisionProperties.Layers.NpcCharacterSensitive
            }
        };
    }

}
