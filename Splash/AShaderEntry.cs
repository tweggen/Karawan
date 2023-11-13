#if false
using System;

namespace Splash;

public abstract class AShaderEntry : IDisposable
{
    public SplashFragmentShader FragmentShader;
    
    public abstract void Dispose();
    public abstract bool IsUploaded();

    public AShaderEntry(SplashFragmentShader fragmentShader)
    {
        FragmentShader = fragmentShader;
    }
}
#endif