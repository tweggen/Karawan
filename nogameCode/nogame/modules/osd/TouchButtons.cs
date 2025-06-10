using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.joyce;
using engine.joyce.components;
using engine.news;

namespace nogame.modules.osd;

public class TouchButtons : AModule
{
    private engine.joyce.TransformApi _aTransform;
    
    private float ButtonSize = 15f;
    private int ButtonsPerRow = 12;
    private int ButtonsPerColumn = (int) Single.Round(12f * 9f / 16f);
    private float ButtonOffsetX = 0.5f;
    private float ButtonOffsetY = 0.5f;

    private List<DefaultEcs.Entity> _buttons;
    
    private InstanceDesc _createButtonMesh(string tagTexture)
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

    private void _setButtonTransforms(DefaultEcs.Entity e, int x, int y)
    {
        Vector3 v3StepX = Vector3.UnitX * (2f / ButtonsPerRow);
        Vector3 v3StepY = -Vector3.UnitY * (1.125f / ButtonsPerColumn);
        Vector3 v3Pos0 =
            Vector3.Zero
            + Vector3.UnitX * (-1f + (2f*(0.5f+ButtonOffsetX)) / ButtonsPerRow)
            + Vector3.UnitY * (9f/16f - (2f*9f/16f*(0.5f+ButtonOffsetY)) / ButtonsPerColumn)
            + Vector3.UnitZ * 2f; 

        _aTransform.SetTransforms(
            e, true, 0x01000000, 
            Quaternion.Identity, 
            v3Pos0 + v3StepX * x + v3StepY * y,
            Vector3.One * (0.5f*4f/ButtonSize)
        );
   
    }


    private DefaultEcs.Entity _createButton(string tagButton, int x, int y, 
        Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event> func)
    {
        var eSettings = _engine.CreateEntity(tagButton);
        eSettings.Set(new Instance3(_createButtonMesh(tagButton)));
        _setButtonTransforms(eSettings, x, y);
        eSettings.Set(new Clickable { CameraLayer = 24, ClickEventFactory = func, Flags = Clickable.ClickableFlags.AlsoOnRelease});
        return eSettings;
    }


    protected override void OnModuleDeactivate()
    {
        I.Get<Engine>().AddDoomedEntities(_buttons);
        _buttons = null;
    }

    
    protected override void OnModuleActivate()
    {        
        _aTransform = I.Get<engine.joyce.TransformApi>();

        _buttons = new();
        
        _buttons.Add(_createButton("but_left.png",  0, ButtonsPerColumn-2,
            (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "a")));
        _buttons.Add(_createButton("but_right.png", 1, ButtonsPerColumn-2,
            (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "d")));
        _buttons.Add(_createButton("but_getinout.png", ButtonsPerRow-2, ButtonsPerColumn-4,
            (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "f")));
        _buttons.Add(_createButton("but_accel.png", ButtonsPerRow-2, ButtonsPerColumn-2,
            (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "w")));
        _buttons.Add(_createButton("but_brake.png", ButtonsPerRow-3, ButtonsPerColumn-2,
            (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "s")));
    }
}