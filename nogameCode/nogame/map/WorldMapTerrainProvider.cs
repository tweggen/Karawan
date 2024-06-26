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
public class WorldMapTerrainProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        
    }

    private builtin.tools.ColorBlender _groundColorBlender = new()
    {
        MapColors = {
            { 0f, new (16f/256f, 32f/256f, 32f/256f, 1f) },
            { 0.5f,new(48f/256f, 64f/256f, 64f/256f, 1f) },
            { 1.0f, new(80f/256f, 80f/256f, 64f/256f, 1f) }
        }
    };


    static private float MapColorMinHeight = -64f; 
    static private float MapColorMaxHeight = 600f; 
    private uint _heightColor(float height)
    {
        float normalizedHeight = (height - MapColorMinHeight) / (MapColorMaxHeight - MapColorMinHeight);
        normalizedHeight = Single.Max(0f, Single.Min(1f, normalizedHeight));
        _groundColorBlender.GetColor(normalizedHeight, out uint col);
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
        var network = I.Get<nogame.intercity.Network>();
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