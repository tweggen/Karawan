using System;

namespace Splash;

public class Flags
{
    public enum GLAnimBuffers
    {
        AnimSSBO = 0,
        AnimUBO = 1
    };

    [Flags]
    public enum AnimBatching
    {
        ByFrameno = 1,
        ByAnimation = 2
    };
}