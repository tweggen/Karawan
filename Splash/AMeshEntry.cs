using System;
using System.Numerics;

namespace Splash;


public abstract class AMeshEntry : IDisposable
{
    public readonly AMeshParams Params;

    public abstract bool IsUploaded();

    public abstract bool IsFilled();
    
    public abstract void Dispose();

    protected AMeshEntry(in AMeshParams p)
    {
        Params = p;
    }
}