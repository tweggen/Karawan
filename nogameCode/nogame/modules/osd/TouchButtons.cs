using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    
    static private InstanceDesc _createButtonMesh(string tagTexture, uint emissiveColor)
    {
        var mesh = engine.joyce.mesh.Tools.CreatePlaneMesh(tagTexture, 
            Vector2.One, Vector2.Zero, Vector2.One);
        var material = I.Get<ObjectRegistry<Material>>().FindLike(new Material()
        {
            EmissiveTexture = I.Get<TextureCatalogue>().FindTexture(tagTexture),
            HasTransparency = true,
            EmissiveColor = emissiveColor
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


    static public DefaultEcs.Entity CreateLayer(string tagButton, bool isHighlighted )
    {
        var e = I.Get<Engine>().CreateEntity(tagButton);
        e.Set(new Instance3(_createButtonMesh(tagButton, isHighlighted?0xff443333:0x00000000)));
        return e;
    }


    static public DefaultEcs.Entity CreateButton(string tagButton, int x, int y, 
        Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event> func)
    {
        var eSettings = I.Get<Engine>().CreateEntity(tagButton);
        var eNormal = CreateLayer(tagButton, false);
        var eHighlighted = CreateLayer(tagButton, true);
        var clickable = new Clickable
        {
            CameraLayer = 24,
            ClickEventFactory = (e, ev, v2RelPos) =>
            {
                I.Get<TransformApi>().SetVisible(eNormal, !ev.IsPressed);
                I.Get<TransformApi>().SetVisible(eHighlighted, ev.IsPressed);

                return func(e, ev, v2RelPos);
            },
            Flags = Clickable.ClickableFlags.AlsoOnRelease
        }; 
        _setButtonTransforms(eNormal, x, y);
        _setButtonTransforms(eHighlighted, x, y);
        eNormal.Set(clickable);
        eHighlighted.Set(clickable);;
        I.Get<TransformApi>().SetTransforms(
            eSettings, true, 0, 
            Quaternion.Identity,Vector3.Zero, Vector3.One
        );
        I.Get<HierarchyApi>().SetParent(eNormal, eSettings);
        I.Get<HierarchyApi>().SetParent(eHighlighted, eSettings);;
        I.Get<TransformApi>().SetVisible(eNormal, true);
        I.Get<TransformApi>().SetVisible(eHighlighted, false);
        
        return eSettings;
    }
}