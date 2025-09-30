using System;
using System.Numerics;

namespace Splash;


public abstract class AMeshEntry : IDisposable
{
    public readonly AMeshParams Params;

    public override int GetHashCode()
    {
        return Params.GetHashCode();
    }

    public abstract bool IsUploaded();

    public abstract bool IsFilled();
    
    public abstract void Dispose();

    protected AMeshEntry(in AMeshParams p)
    {
        Params = p;
    }
}