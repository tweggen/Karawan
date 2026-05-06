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
            AnimationPackName = "full",
            CPUNodes = "MiddleFinger2_R;MiddleFinger2_L",
            ModelBaseBone = "Root_M",
            ModelUrl = "man_casual_Rig.fbx",
            Scale = "1"
        };
    }

}
