using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.world;
using builtin.map;
using engine;
using engine.joyce;
using engine.streets;
using static engine.Logger;

namespace nogame.map;


/**
 * Create an pixel image of the entire world.
 */
public class WorldMapClusterProvider : IWorldMapProvider
{
    public float ClusterNameY { get; set; } = 250f;
    public void WorldMapCreateEntities(engine.Engine engine0, Entity parentEntity, uint cameraMask)
    {
        var clusterList = engine.world.ClusterList.Instance();
        foreach (var clusterDesc in clusterList.GetClusterList())
        {
            float width = 240f;
            DefaultEcs.Entity eCity = engine0.CreateEntity($"nogame.map.city {clusterDesc.Name}");
            I.Get<TransformApi>().SetTransforms(eCity, true, 0x00800000, 
                Quaternion.Identity, clusterDesc.Pos with { Y = ClusterNameY });
            eCity.Set(new engine.draw.components.OSDText(
                new Vector2(0f , 0f),
                new Vector2(width, 18f),
                $"{clusterDesc.Name}",
                10,
                0xffeeaa22,
                0x00000000,
                HAlign.Left)
            {
                MaxDistance = 100000f
            });
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
        }
        
        target.EndModification();
    }
    
    
    public void WorldMapCreateBitmap(engine.Engine engine0, IFramebuffer target)
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