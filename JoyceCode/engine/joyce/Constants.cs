namespace engine.joyce;

/**
 * Constants shared across the 3D rendering system.
 * These must match the corresponding values in the shaders.
 */
public static class Constants
{
    /**
     * Maximum number of bones supported per skeleton.
     * This must match MAX_BONES in LIghtingVS.vert.
     */
    public const int MaxBones = 120;

    /**
     * Maximum number of bone influences per vertex.
     * This must match MAX_BONE_INFLUENCE in LIghtingVS.vert.
     */
    public const int MaxBoneInfluence = 4;
}