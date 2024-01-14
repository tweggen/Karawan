namespace builtin.map;

public interface IFragmentMapProvider
{
    /**
     * Create the entities representing another map fragment, all child
     * entities of the given parent entity.
     */
    public void FragmentMapCreateEntities(
        engine.world.Fragment worldFragment,
        uint cameraMask);
}