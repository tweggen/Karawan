using System;
using System.Numerics;

namespace Splash;

public class AMeshParams
{
    public engine.joyce.Mesh JMesh;
    public Vector2 UVOffset;
    public Vector2 UVScale;
}


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