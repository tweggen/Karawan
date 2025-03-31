using System;

namespace Splash;

public class Flags
{
    public enum GLAnimBuffers
    {
        AnimSSBO = 0,
        AnimUniform = 1,
        AnimUBO = 2,
    };

    [Flags]
    public enum AnimBatching
    {
        ByFrameno = 1,
        ByAnimation = 2
    };
}