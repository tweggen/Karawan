using System;
using System.Numerics;

namespace Splash;

public abstract class AAnimationsEntry : IDisposable
{
    public readonly engine.joyce.Model? Model;

    public abstract bool IsUploaded();

    public abstract bool IsFilled();
    
    public abstract void Dispose();

    protected AAnimationsEntry(in engine.joyce.Model? m)
    {
        Model = m;
    }
}


