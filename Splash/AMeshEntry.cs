using System;

namespace Splash;

public abstract class AMeshEntry
{
    public readonly engine.joyce.Mesh JMesh;

    public abstract bool IsUploaded();
    
    protected AMeshEntry(in engine.joyce.Mesh jMesh)
    {
        JMesh = jMesh;
    }
}