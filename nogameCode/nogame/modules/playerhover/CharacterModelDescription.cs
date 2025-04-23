using engine.joyce;

namespace nogame.modules.playerhover;

public class CharacterModelDescription
{
    // Man B|Idle 01
    // Man B|Run Mid
    // Man B|Child Walk Happy

    //public string AnimName { get; set; } = "Walk_Loop";
    //public string ModelUrl { get; set; } = "player.glb";
    public string WalkAnimName { get; set; } = "Man B|Child Walk Happy";
    public string RunAnimName { get; set; } = "Man B|Run Mid";
    public string IdleAnimName { get; set; } = "Man B|Idle 01";
    
    public string ModelUrl { get; set;  } = "Studio Ochi Spring Man B_ANIM.fbx";
        
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.ROTATE_Y180
                                              | InstantiateModelParams.ROTATE_X90
        ;
}