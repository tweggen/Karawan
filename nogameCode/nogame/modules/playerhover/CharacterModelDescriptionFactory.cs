using engine.joyce;

namespace nogame.modules.playerhover;

public static class CharacterModelDescriptionFactory
{
    public static nogame.characters.CharacterModelDescription CreatePolyperfectWalkPlayer()
    {
        return new()
        {
            WalkAnimName = "Walk_Male",
            RunAnimName = "Run_InPlace",
            IdleAnimName = "Idle_Generic",
            JumpAnimName = "Standing_Jump",
            PunchRightAnim = "Punch_RightHand",
            PunchLeftAnim = "Punch_LeftHand",
            AnimationUrls = "Idle_Generic.fbx;Run_InPlace.fbx;Walk_Male.fbx;Running_Jump.fbx;Standing_Jump.fbx;Punch_LeftHand.fbx;Punch_RightHand.fbx;Death_FallForwards.fbx",
            CPUNodes = "MiddleFinger2_R;MiddleFinger2_L",
            ModelBaseBone = "Root_M",
            ModelUrl = "man_casual_Rig.fbx",
            Scale = "1",
            ModelGeomFlags = 0
            // | InstantiateModelParams.ROTATE_X90
            // | InstantiateModelParams.ROTATE_X180
            //| InstantiateModelParams.ROTATE_Y180
            ,
        };
    }

}
