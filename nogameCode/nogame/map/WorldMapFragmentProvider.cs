using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.world;
using builtin.map;
using engine;
using engine.world;
using engine.streets;
using static engine.Logger;

namespace nogame.map;


/**
 * Create an pixel image of the entire 
 */
public class WorldMapFragmentProvider : IWorldMapProvider
{
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
    }


    private void _drawLine(IFramebuffer target, Context context, 
        in Matrix3x2 m2fb, 
        in Vector2 posA, in Vector2 posB,
        float width)
    {
        Vector2[] poly = new Vector2[2];

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
        int nx = (int)((MetaGen.MaxWidth+MetaGen.FragmentSize-1)/(MetaGen.FragmentSize));
        int ny = (int)((MetaGen.MaxHeight+MetaGen.FragmentSize-1)/(MetaGen.FragmentSize));
        
        Context dcGrid = new Context();
        dcGrid.FillColor = 0xff444444;
        
        for (int ix=-nx/2+1; ix<nx/2; ++ix)
        {
            float x = ix * MetaGen.FragmentSize - MetaGen.FragmentSize / 2f;
            _drawLine(target, dcGrid, m2fb, 
                new Vector2(x, -MetaGen.MaxHeight / 2f),
                new Vector2(x, +MetaGen.MaxHeight / 2f-1),
                1f);    
        }
        for (int iy=-ny/2; iy<ny/2; ++iy)
        {
            float y = iy * MetaGen.FragmentSize - MetaGen.FragmentSize / 2f;
            _drawLine(target, dcGrid, m2fb, 
                new Vector2(-MetaGen.MaxWidth / 2f, y),
                new Vector2(+MetaGen.MaxWidth / 2f - 1, y), 
                1);
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