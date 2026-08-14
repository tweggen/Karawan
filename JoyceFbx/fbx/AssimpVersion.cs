namespace builtin.loader.fbx;

/// <summary>
/// Enum to track which version of Assimp is being used for FBX animation loading.
/// Different versions have different behaviors and require compensation in baking/loading code.
///
/// Moved out of engine.joyce by WP-4.4 along with the importer. The plan's
/// definition of done says to DELETE this and AssimpVersionDetector; that is
/// wrong in one respect and is deliberately not followed. FbxModel still uses the
/// detected version to compensate the bone offset matrices at load time, so
/// deleting it would not remove a dead concept, it would silently change the
/// geometry the bake produces. What the DoD is actually after - no Assimp in the
/// shipped app - is achieved by this type living in JoyceFbx, which no runtime
/// target references.
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
