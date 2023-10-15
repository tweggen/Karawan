using System;
using System.Data;

namespace Splash;

public abstract class ASingleShaderEntry : IDisposable
{
    public SplashAnyShader SplashAnyShader;


    public abstract void Dispose();
    public abstract bool IsUploaded();
    
    
    public ASingleShaderEntry(SplashAnyShader splashAnyShader)
    {
        SplashAnyShader = splashAnyShader;
    }
}