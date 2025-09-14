using engine.joyce;

namespace nogame.modules.playerhover;

public class CharacterModelDescription
{
    // Man B|Idle 01
    // Man B|Run Mid
    // Man B|Child Walk Happy

    #if false
    public string AnimName { get; set; } = "Walk_Loop";
    public string ModelUrl { get; set; } = "player.glb";
    #endif
    
    #if false
    /*
     * Studio Ochi
     */
    public string WalkAnimName { get; set; } = "Metarig Man B|Child Walk Happy";
    public string RunAnimName { get; set; } = "Metarig Man B|Run Mid";
    public string IdleAnimName { get; set; } = "Metarig Man B|Idle 01";
    public string JumpAnimName { get; set; } = "MetardAnimig Man B|Run Mid";
    
    public string ModelUrl { get; set;  } = "Studio Ochi Spring Man B_ANIM.fbx";
    public string AnimationUrls { get; set; } = "";
    public string Scale { get; set; } = "1";
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.ROTATE_Z180
                                              ;

    #endif
    
    #if false
    /*
     * mixamo
     */
    public string WalkAnimName { get; set; } = "walking";
    public string RunAnimName { get; set; } = "running";
    public string IdleAnimName { get; set; } = "idle";
    public string JumpAnimName { get; set; } = "jumping";
    public string AdditionalUrls { get; set; } = "cover to stand (2).fbx;cover to stand.fbx;crouched sneaking left.fbx;crouched sneaking right.fbx;falling idle.fbx;falling to roll.fbx;hard landing.fbx;idle (2).fbx;idle (3).fbx;idle (4).fbx;idle (5).fbx;idle.fbx;jumping up.fbx;left cover sneak.fbx;left turn.fbx;right cover sneak.fbx;right turn.fbx;run to stop.fbx;running.fbx;stand to cover (2).fbx;stand to cover.fbx;walking.fbx";
    
    public string ModelUrl { get; set;  } = "mixamo_ochi_man_b.fbx";
    public string Scale { get; set; } = "1";
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.ROTATE_Y180
                                              | InstantiateModelParams.ROTATE_X90
        ;

    #endif

    #if true
    /*
     * polyperfect
     */
    public string WalkAnimName { get; set; } = "Walk_Male";
    public string RunAnimName { get; set; } = "Run_InPlace";
    public string IdleAnimName { get; set; } = "Idle_Generic";
    public string JumpAnimName { get; set; } = "Standing_Jump";

    public string AnimationUrls { get; set; } = "Idle_Generic.fbx;Run_InPlace.fbx;Walk_Male.fbx;Standing_Jump.fbx";
        // "Idle_Generic.fbx;Run_InPlace.fbx;Walk_Male.fbx;Standing_Jump.fbx";
        // "Idle_Generic.fbx;Idle_HardDay.fbx;Idle_Texting.fbx;Idle_Waving.fbx;Kick_LeftFoot.fbx;Punch_LeftHand.fbx;Punch_RightHand.fbx;Run_InPlace.fbx;Running_Jump.fbx;Standing_Jump.fbx;Walk_InPlace_Female.fbx;Walk_Left.fbx;Walk_Male.fbx"; 
    
    public string ModelUrl { get; set;  } = "man_casual_Rig.fbx";

    public string Scale { get; set; } = "1";

    public int ModelGeomFlags { get; set; } = 0
                                              // | InstantiateModelParams.ROTATE_X90
                                              // | InstantiateModelParams.ROTATE_X180
                                              //| InstantiateModelParams.ROTATE_Y180
        ;
#endif

    
    public DefaultEcs.Entity EntityAnimations { get; set; } = default;
    public Model Model { get; set; } = null;
}
