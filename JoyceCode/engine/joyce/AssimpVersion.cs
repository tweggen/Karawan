namespace engine.joyce;

/// <summary>
/// Enum to track which version of Assimp is being used for FBX animation loading.
/// Different versions have different behaviors and require compensation in baking/loading code.
/// </summary>
public enum AssimpVersion
{
    /// <summary>
    /// Assimp 5.4.1 (via Silk.NET 2.22.0)
    /// Animation behavior: Known to work correctly with current baking code.
    /// </summary>
    Assimp5_4_1,

    /// <summary>
    /// Assimp 6.0.2 (via Silk.NET 2.23.0)
    /// Animation behavior: Introduces frame counting and keyframe insertion changes.
    /// Requires compensation in BakeAnimations() and FBX loading code.
    /// </summary>
    Assimp6_0_2,
}
