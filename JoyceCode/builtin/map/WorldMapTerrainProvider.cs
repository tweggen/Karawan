using DefaultEcs;
using engine.draw;
using engine.world;

namespace builtin.map;

public class WorldMapTerrainProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        throw new System.NotImplementedException();
    }

    /**
     * We just render the terrain height.
     */
    public void WorldMapCreateBitmap(IFramebuffer target)
    {
        /*
         * We scale the map to fit the framebuffer
         */
        float worldMinX = -MetaGen.MaxWidth/2f;
        float worldMinZ = -MetaGen.MaxHeight/2f;
        
        // how to refer to skeleton ground from here.
    }
}