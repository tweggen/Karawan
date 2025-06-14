using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.joyce;
using engine.joyce.components;

namespace nogame.modules.osd;

public class TouchButtons
{
    public static float ButtonSize = 15f;
    public static int ButtonsPerRow = 12;
    public static int ButtonsPerColumn = (int) Single.Round(12f * 9f / 16f);
    public static float ButtonOffsetX = 0.5f;
    public static float ButtonOffsetY = 0.5f;

    static private InstanceDesc _createButtonMesh(string tagTexture)
    {
        var mesh = engine.joyce.mesh.Tools.CreatePlaneMesh(tagTexture, 
            Vector2.One, Vector2.Zero, Vector2.One);
        var material = I.Get<ObjectRegistry<Material>>().FindLike(new Material()
        {
            EmissiveTexture = I.Get<TextureCatalogue>().FindTexture(tagTexture),
            HasTransparency = true
        });
        InstanceDesc id = new(
            new List<Mesh>() { mesh }, 
            new List<int>() { 0 }, 
            new List<engine.joyce.Material>() { material },
            new List<engine.joyce.ModelNode>() { null }, 
            20f );
        return id;
    }

    static private void _setButtonTransforms(DefaultEcs.Entity e, int x, int y)
    {
        Vector3 v3StepX = Vector3.UnitX * (2f / ButtonsPerRow);
        Vector3 v3StepY = -Vector3.UnitY * (1.125f / ButtonsPerColumn);
        Vector3 v3Pos0 =
            Vector3.Zero
            + Vector3.UnitX * (-1f + (2f*(0.5f+ButtonOffsetX)) / ButtonsPerRow)
            + Vector3.UnitY * (9f/16f - (2f*9f/16f*(0.5f+ButtonOffsetY)) / ButtonsPerColumn)
            + Vector3.UnitZ * 2f; 

        I.Get<TransformApi>().SetTransforms(
            e, true, 0x01000000, 
            Quaternion.Identity, 
            v3Pos0 + v3StepX * x + v3StepY * y,
            Vector3.One * (0.5f*4f/ButtonSize)
        );
   
    }


    static public DefaultEcs.Entity CreateButton(string tagButton, int x, int y, 
        Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event> func)
    {
        var eSettings = I.Get<Engine>().CreateEntity(tagButton);
        eSettings.Set(new Instance3(_createButtonMesh(tagButton)));
        _setButtonTransforms(eSettings, x, y);
        eSettings.Set(new Clickable { CameraLayer = 24, ClickEventFactory = func, Flags = Clickable.ClickableFlags.AlsoOnRelease});
        return eSettings;
    }



}