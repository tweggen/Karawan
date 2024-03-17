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
public class WorldMapFragments : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        
    }


    private void _drawLine(IFramebuffer target, Context context, 
        in Matrix3x2 m2fb, 
        in Vector2 posA, in Vector2 posB,
        float width)
    {
        Vector2[] poly = new Vector2[4];

        poly[0] = posA;
        poly[1] = posB;
        for (int i = 0; i < poly.Length; ++i)
        {
            poly[i] = Vector2.Transform(poly[i], m2fb);
        }

        target.DrawPoly(context, poly);
    }


    private void _drawFragmentGrid(IFramebuffer target, in Matrix3x2 m2fb)
    {
        int nx = (int)((world.MetaGen.MaxWidth+world.MetaGen.FragmentSize-1)/(world.MetaGen.FragmentSize));
        int ny = (int)((world.MetaGen.MaxHEight+world.MetaGen.FragmentSize-1)/(world.MetaGen.FragmentSize));
        
        Context dcGrid = new Context();
        dcHighway.Color = 0xff444444;
        

        for (int ix=1; ix<nx; ++ix)
        {
            _drawLine(target, m2, dcGrid, new Vector2(ix*world.MetaGen.FragmentSize
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
        

        _drawFragmentGrid(target, m2fb);

        
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