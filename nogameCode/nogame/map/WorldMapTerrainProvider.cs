using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.world;
using builtin.map;
using engine;
using engine.streets;
using static engine.Logger;

namespace nogame.map;

public class WorldMapTerrainProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        throw new System.NotImplementedException();
    }


    static private float MapColorMinHeight = -64f; 
    static private float MapColorMaxHeight = 192f; 
    private uint _heightColor(float height)
    {
        float normalizedHeight = (height - MapColorMinHeight) / (MapColorMaxHeight - MapColorMinHeight);
        normalizedHeight = Single.Max(0f, Single.Min(1f, normalizedHeight));
        /*
         * normalizedheight now ranges from 0 to 1.
         */
        
        int heightCol = (int)(normalizedHeight * 255f);
        heightCol = Int32.Min(255, heightCol);
        heightCol = Int32.Max(0, heightCol);

        byte blue = (byte)Int32.Min(255, heightCol );
        byte others = (byte)Int32.Max(0, heightCol - 128);
        uint col = 0xff000000 | ((uint)blue << 16) | ((uint)others << 8) | ((uint)others);
        return col;
    }


    private void _drawThickLine(IFramebuffer target, Context context, 
        in Matrix3x2 m2fb, 
        in Vector2 posA, in Vector2 posB,
        float width)
    {
        Vector2 w2 = new(width / 2f, width / 2f);
        Vector2 u = posB - posA;
        float l = u.Length();
        if (l < 1f)
        {
            target.FillRectangle(context,
                Vector2.Transform(posA-w2, m2fb),
                Vector2.Transform(posA+w2, m2fb)-Vector2.One);
        }
        else
        {
            Vector2[] poly = new Vector2[4];

            u /= l;
            Vector2 v = new(u.Y, -u.X);
            Vector2 vw2 = v * width / 2f;
            poly[0] = posA - vw2;
            poly[1] = posB - vw2;
            poly[2] = posB + vw2;
            poly[3] = posA + vw2;
            for (int i = 0; i < poly.Length; ++i)
            {
                poly[i] = Vector2.Transform(poly[i], m2fb);
            }

            target.DrawPoly(context, poly);
        }
    }


    private void _drawIntercityLines(IFramebuffer target, in Matrix3x2 m2fb)
    {
        var network = Implementations.Get<nogame.intercity.Network>();
        var lines = network.Lines;

        Context dcHighway = new Context();
        dcHighway.FillColor = 0xff441144;
        dcHighway.Color = 0xff441144;

        foreach (var line in lines)
        {
            _drawThickLine(target, dcHighway, m2fb, 
                new Vector2(line.StationA.Position.X, line.StationA.Position.Z),
                new Vector2(line.StationB.Position.X, line.StationB.Position.Z), 32f);
        }
    }


    private void _drawClusterBase(IFramebuffer target, ClusterDesc clusterDesc,
        in Matrix3x2 m2fb)
    {
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;

        engine.draw.Context dc = new();
        
        Vector2 sizehalf = new Vector2(
            clusterDesc.Size / MetaGen.MaxWidth * fbWidth / 2f,
            clusterDesc.Size / MetaGen.MaxHeight * fbHeight / 2f);

        Vector2 pos = Vector2.Transform(clusterDesc.Pos2, m2fb); 
            
        dc.FillColor = 0xff441144;
        target.FillRectangle(dc, pos-sizehalf, pos+sizehalf);
    }
    
    
    private void _drawClusterText(IFramebuffer target, ClusterDesc clusterDesc,
        in Matrix3x2 m2fb)
    {
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;

        engine.draw.Context dc = new();
        
        Vector2 sizehalf = new Vector2(clusterDesc.Size / MetaGen.MaxWidth * fbWidth / 2f,
            clusterDesc.Size / MetaGen.MaxHeight * fbHeight / 2f);

        Vector2 pos = Vector2.Transform(clusterDesc.Pos2, m2fb); 
            
        dc.FillColor = 0xff441144;
        dc.TextColor = 0xff22aaee;
        target.DrawText(dc, 
            new Vector2(pos.X, pos.Y-10f), 
            new Vector2(pos.X+100, pos.Y+2f), 
            clusterDesc.Name, 12);
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

        engine.draw.Context dc = new();
        
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;
        

        dc.FillColor = 0xff000000;
        target.FillRectangle(dc, new Vector2(0, 0), new Vector2( fbWidth, fbHeight) );

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
                target.FillPoly(dc, polyPoints);
                
                /*
                 * Now the average of those next to us. 
                 */
                setDiamond((float)x+0.5f, (float)y+0.5f, ref polyPoints);
                height = skeleton[y, x] + skeleton[y, x + 1] + skeleton[y + 1, x] + skeleton[y + 1, x + 1];
                height /= 4f;
                height = (16f+height) * 4f;
                col = _heightColor(height);
                dc.FillColor = col;
                target.FillPoly(dc, polyPoints);
            }
        }
        
        /*
         * Draw the clusters.
         */
        Matrix3x2 m2fb = Matrix3x2.CreateScale(
            new Vector2(
                fbWidth / MetaGen.MaxWidth,
                fbHeight / MetaGen.MaxHeight));
        m2fb *= Matrix3x2.CreateTranslation(
            fbWidth/2f, fbHeight/2f);
        
        Vector2 worldMin = new(-MetaGen.MaxWidth / 2f, -MetaGen.MaxHeight / 2f);


        _drawIntercityLines(target, m2fb);

        var clusterList = engine.world.ClusterList.Instance();
        foreach (var clusterDesc in clusterList.GetClusterList())
        {
            _drawClusterBase(target, clusterDesc, m2fb);
            _drawClusterText(target, clusterDesc, m2fb);

#if false
            StrokeStore strokeStore = null;            
            try
            {
                strokeStore = clusterDesc.StrokeStore();
            }
            catch (Exception e)
            {
                Error($"Exception while retrieving description of stroke store for cluster {clusterDesc.Name}: {e}");
            }

            if (strokeStore != null)
            {
                foreach (var stroke in strokeStore.GetStrokes())
                {
                    float weight = stroke.Weight;
                    //float weight = 4f; 
                    bool isPrimary = stroke.IsPrimary;

                    Vector2 posA = stroke.A.Pos + clusterDesc.Pos2;
                    Vector2 posB = stroke.B.Pos + clusterDesc.Pos2;
                    Vector2 u = posB - posA;
                    u /= u.Length();
                    Vector2 v = new(u.Y, -u.X);
                    Vector2 vw2 = v * weight / 2f;
                    streetPoly[0] = posA - vw2;
                    streetPoly[1] = posB - vw2;
                    streetPoly[2] = posB + vw2;
                    streetPoly[3] = posA + vw2;
                    for (int i = 0; i < streetPoly.Length; ++i)
                    {
                        streetPoly[i] = Vector2.Transform(streetPoly[i], m2fb);
                    }
                    target.DrawPoly(dcStreets, streetPoly);
                }
            }
#endif
        }
        
        target.EndModification();
    }
    
    /**
     * We just render the terrain height.
     */
    public void WorldMapCreateBitmap(IFramebuffer target)
    {
        try
        {
            _createBitmap(target);
        }
        catch (Exception e)
        {
            Trace( $"Error creating map bitmap: {e}");
        }
    }
}