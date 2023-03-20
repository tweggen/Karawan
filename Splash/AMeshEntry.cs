namespace Karawan.platform.cs1.splash;

public abstract class AMeshEntry
{
    public readonly engine.joyce.Mesh JMesh;

    public abstract bool IsMeshUploaded();

    protected AMeshEntry(in engine.joyce.Mesh jMesh)
    {
        JMesh = jMesh;
    }
}