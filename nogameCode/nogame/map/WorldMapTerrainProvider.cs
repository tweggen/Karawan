using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.world;
using builtin.map;

namespace nogame.map;

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
        target.BeginModification();
        
        /*
         * We have a grid of height sample in our skeleton map.
         * While drawing this layer of terrain, 
         */
        /*
         * We scale the map to fit the framebuffer
         */
        float worldMinX = -MetaGen.MaxWidth/2f;
        float worldMinZ = -MetaGen.MaxHeight/2f;
        
        /*
         * Read all data we need to scale our drawing.
         */
        terrain.GroundOperator groundOperator = terrain.GroundOperator.Instance();
        int skeletonWidth = groundOperator.SkeletonWidth;
        int skeletonHeight = groundOperator.SkeletonHeight;
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;

        engine.draw.Context dc = new();

        dc.FillColor = 0xff883322;
        target.FillRectangle(dc, new Vector2(0, 0), new Vector2( fbWidth-1, fbHeight-1) );
        
        target.EndModification();
    }
}