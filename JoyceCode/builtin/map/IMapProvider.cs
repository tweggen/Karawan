namespace builtin.map;

public interface IMapProvider
{
    /**
     * obtain a world overview map.
     * The world overview map is a basis for more detailed maps.
     * The system will render the world overview map first, only add
     * world fragment maps on top of the world overview map.
     *
     * The world map quality, by definition, shall be suitable
     * to held in (graphics) memory.
     *
     * TODO: The world map might change, so an update strategy is required.
     *
     * Map scale is arbitrary, but we recommend the same as the standard
     * game scale, i.e. 1 represents 1 meter.
     */
    public void WorldMapCreateEntities(
        DefaultEcs.Entity parentEntity,
        uint cameraMask);
    
    /**
     * Create the entities representing another map fragment, all child
     * entities of the given parent entity.
     */
    public void FragmentMapCreateEntities(
        engine.world.Fragment worldFragment,
        DefaultEcs.Entity parentEntity,
        uint cameraMask);
}