using System.Numerics;
using builtin.tools;
using engine.geom;

namespace nogame.characters.citizen;

using engine.joyce;

public static class CharacterModelDescriptionFactory
{
    private static readonly string [] _arrModels = 
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

    public static nogame.characters.CharacterModelDescription CreateCitizen(RandomSource rnd)
    {
        string strModel;
        int idxModel = (int)(rnd.GetFloat() * _arrModels.Length);
        strModel = _arrModels[idxModel];        
        bool isMale = strModel.StartsWith("man");
        float propMaxDistance = (float)engine.Props.Get("nogame.characters.citizen.maxDistance", 100f);
        
        return new()
        {
            WalkAnimName = isMale?"Walk_Male":"Walk_InPlace_Female",
            RunAnimName = "Run_InPlace",
            DeathAnimName = "Death_FallForwards",
            AnimationUrls = $"Run_InPlace.fbx;{(isMale?"Walk_Male.fbx":"Walk_InPlace_Female.fbx")};Death_FallForwards.fbx",
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
                CollisionLayers = 0x0002,
            }
        };
    }

}
