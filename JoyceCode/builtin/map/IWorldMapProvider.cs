using engine.draw;

namespace builtin.map;

public interface IWorldMapProvider
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
     * Render a bitmap of the world map into the given framebuffer.
     * The World size as specified in the world generator applies.
     * The implementation is free not to render anything at all.
     * In that case, only the entities created on top will be rendered.
     */
    public void WorldMapCreateBitmap(IFramebuffer target);
}