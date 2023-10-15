using System;

namespace Splash;

public abstract class AMeshEntry : IDisposable
{
    public readonly engine.joyce.Mesh JMesh;

    public abstract bool IsUploaded();

    public abstract void Dispose();

    protected AMeshEntry(in engine.joyce.Mesh jMesh)
    {
        JMesh = jMesh;
    }
}