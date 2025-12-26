namespace engine.behave;

public interface IEntityStrategy
{
    /**
     * Called after a given period of inactivity: Sync with reality before
     * continueing your behavior.
     */
    public void Sync(in DefaultEcs.Entity entity);

    /**
     * On Detach
     */
    public void OnDetach(in DefaultEcs.Entity entity);

    /**
     * Called after the behavior has been attached to this entity.
     */
    public void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity);
}