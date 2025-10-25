using engine.joyce;

namespace nogame.characters;

public class CharacterModelDescription
{
    public string WalkAnimName { get; set; } //= "Walk_Male";
    public string RunAnimName { get; set; } //= "Run_InPlace";
    public string IdleAnimName { get; set; } // "Idle_Generic";
    public string JumpAnimName { get; set; } //= "Standing_Jump";

    public string PunchRightAnim { get; set; } //= "Punch_RightHand";
    public string PunchLeftAnim { get; set; } //= "Punch_LeftHand";
    
    public string AnimationUrls { get; set; } //= "Idle_Generic.fbx;Run_InPlace.fbx;Walk_Male.fbx;Running_Jump.fbx;Standing_Jump.fbx;Punch_LeftHand.fbx;Punch_RightHand.fbx;Death_FallForwards.fbx";

    public string CPUNodes { get; set; } //= "MiddleFinger2_R;MiddleFinger2_L";

    public string ModelBaseBone { get; set; } //= "Root_M";
    
    public string ModelUrl { get; set;  } //= "man_casual_Rig.fbx";

    public string Scale { get; set; } = "1";

    public int ModelGeomFlags { get; set; } = 0
                                              // | InstantiateModelParams.ROTATE_X90
                                              // | InstantiateModelParams.ROTATE_X180
                                              //| InstantiateModelParams.ROTATE_Y180
        ;
    
    public DefaultEcs.Entity EntityAnimations { get; set; } = default;
    public Model Model { get; set; } = null;
    public AnimationState AnimationState { get; set; } = null;
}