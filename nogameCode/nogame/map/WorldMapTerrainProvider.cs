using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.world;
using builtin.map;
using static engine.Logger;

namespace nogame.map;

public class WorldMapTerrainProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        throw new System.NotImplementedException();
    }

    private uint _heightColor(float height)
    {
        int heightCol = (int)(height + 64f);
        heightCol = Int32.Min(255, heightCol);
        heightCol = Int32.Max(0, heightCol);

        byte blue = (byte)Int32.Min(255, heightCol );
        byte others = (byte)Int32.Max(0, heightCol - 128);
        uint col = 0xff000000 | ((uint)blue << 16) | ((uint)others << 8) | ((uint)others);
        return col;
    }
    
    
    private void _createBitmap(IFramebuffer target)
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
        var skeleton = groundOperator.GetSkeleton();
        int skeletonWidth = groundOperator.SkeletonWidth;
        int skeletonHeight = groundOperator.SkeletonHeight;
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;

        engine.draw.Context dc = new();

        dc.FillColor = 0xff000000;
        target.FillRectangle(dc, new Vector2(0, 0), new Vector2( fbWidth-1, fbHeight-1) );

        Vector2[] polyPoints = new Vector2[4];


        var setDiamond = (float fx, float fy, ref Vector2[] pp) =>
        {
            polyPoints[0] = new Vector2( 
                (fx - 0.5f) / (float)skeletonWidth * (float)fbWidth  + 0.5f,
                (fy       ) / (float)skeletonHeight* (float)fbHeight + 0.5f);
            polyPoints[1] = new Vector2(
                (fx       ) / (float)skeletonWidth * (float)fbWidth  + 0.5f,
                (fy - 0.5f) / (float)skeletonHeight* (float)fbHeight + 0.5f);
            polyPoints[2] = new Vector2(
                (fx + 0.5f) / (float)skeletonWidth * (float)fbWidth  + 0.5f,
                (fy       ) / (float)skeletonHeight* (float)fbHeight + 0.5f);
            polyPoints[3] = new Vector2( 
                (fx       ) / (float)skeletonWidth * (float)fbWidth  + 0.5f,
                (fy + 0.5f) / (float)skeletonHeight* (float)fbHeight + 0.5f);
        };

        /*
         * First trivial rendition.
         */
        for (int y = 0; y < skeletonWidth - 1; y++)
        {
            for (int x = 0; x < skeletonHeight - 1; x++)
            {
                /*
                 * First me...
                 */
                setDiamond((float)x, (float)y, ref polyPoints);
                
                /*
                 * We assume normal heights are between -16 and 48, scale it down to 0..255
                 */
                float height = skeleton[y, x];
                height = (16f+height) * 4f;

                uint col = _heightColor(height);
                dc.FillColor = col;
                target.DrawPoly(dc, polyPoints);
                
                /*
                 * Now the average of those next to us. 
                 */
                setDiamond((float)x+0.5f, (float)y+0.5f, ref polyPoints);
                height = skeleton[y, x] + skeleton[y, x + 1] + skeleton[y + 1, x] + skeleton[y + 1, x + 1];
                height /= 4f;
                height = (16f+height) * 4f;
                col = _heightColor(height);
                dc.FillColor = col;
                target.DrawPoly(dc, polyPoints);
                
            }
        }
        
        /*
         * Draw the clusters.
         */
        var clusterList = engine.world.ClusterList.Instance();
        foreach (var clusterDesc in clusterList.GetClusterList())
        {
            float sizehalf = clusterDesc.Size / MetaGen.MaxWidth * fbWidth / 2f;
            float posX = (clusterDesc.Pos.X-worldMinX) / MetaGen.MaxWidth * fbWidth;
            float posY = (clusterDesc.Pos.Z-worldMinZ) / MetaGen.MaxHeight * fbHeight;

            dc.FillColor = 0xff441144;
            target.FillRectangle(dc, new Vector2(posX-sizehalf, posY-sizehalf), new Vector2(posX+sizehalf, posY+sizehalf));
            dc.TextColor = 0xff22aaee;
            target.DrawText(dc, new Vector2(posX, posY-10f), new Vector2(posX+100, posY+2f), clusterDesc.Name, 12);
        }
        
        target.EndModification();
    }
    
    /**
     * We just render the terrain height.
     */
    public void WorldMapCreateBitmap(IFramebuffer target)
    {
        _createBitmap(target);
    }
}