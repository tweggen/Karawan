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
public class WorldMapIntercityProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        
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
        dcHighway.FillColor = 0xff222222;
        dcHighway.Color = 0xff222222;

        foreach (var line in lines)
        {
            _drawThickLine(target, dcHighway, m2fb, 
                new Vector2(line.StationA.Position.X, line.StationA.Position.Z),
                new Vector2(line.StationB.Position.X, line.StationB.Position.Z), 32f);
        }
    }


    private void _createBitmap(IFramebuffer target)
    {
        target.BeginModification();
        
        /*
         * We have a grid of height sample in our skeleton map.
         * While drawing this layer of terrain, 
         */
        /*
         * Read all data we need to scale our drawing.
         */
        terrain.GroundOperator groundOperator = terrain.GroundOperator.Instance();

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
        

        _drawIntercityLines(target, m2fb);

        
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