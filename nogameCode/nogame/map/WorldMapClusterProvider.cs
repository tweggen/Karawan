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


/**
 * Create an pixel image of the entire world.
 */
public class WorldMapClusterProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        throw new System.NotImplementedException();
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
        engine.draw.Context dc = new();
        
        int fbWidth = (int) target.Width;
        int fbHeight = (int) target.Height;
        

        dc.FillColor = 0xff000000;
        /*
         * Draw the clusters.
         */
        Matrix3x2 m2fb = Matrix3x2.CreateScale(
            new Vector2(
                fbWidth / MetaGen.MaxWidth,
                fbHeight / MetaGen.MaxHeight));
        m2fb *= Matrix3x2.CreateTranslation(
            fbWidth/2f, fbHeight/2f);
        
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