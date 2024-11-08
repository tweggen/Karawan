namespace Splash;

public abstract class AResourceEntry
{
    public enum ResourceState
    {
        Created,
        Loading,
        Uploading,
        Using,
        Outdated,
        Dead
    }

    public abstract ResourceState State { get; }
    
    
    public bool IsUploading()
    {
        return State >= ResourceState.Using;
    }

    public bool IsOutdated()
    {
        return State == ResourceState.Outdated;
    }


}