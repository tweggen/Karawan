namespace Karawan.platform.cs1.splash;

public abstract class AMaterialEntry
{
    readonly public engine.joyce.Material JMaterial;

    public abstract bool IsUploaded();
    public abstract bool HasTransparency(); 

    protected AMaterialEntry(in engine.joyce.Material jMaterial)
    {
        JMaterial = jMaterial;
    }
}