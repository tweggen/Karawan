using System;

namespace Splash;

public abstract class AFragmentShaderEntry : IDisposable
{
    public AFragmentShader FragmentShader;

    public abstract void Dispose();
    public abstract bool IsUploaded();
}