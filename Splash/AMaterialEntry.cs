namespace Splash;

public abstract class AMaterialEntry
{
    public readonly  engine.joyce.Material JMaterial;

    public abstract bool IsUploaded();
    public abstract bool IsOutdated();
    public abstract bool HasTransparency(); 

    protected AMaterialEntry(in engine.joyce.Material jMaterial)
    {
        JMaterial = jMaterial;
    }
}