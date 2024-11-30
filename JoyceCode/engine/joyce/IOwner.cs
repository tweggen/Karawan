namespace engine.joyce;

public interface IOwner
{
    /**
     * Dispose all entities owned by this owner.
     * This is necessary, e.g. if the owner is supposed to be unloaded.
     */
    public void DisposeOwnedEntities();
}