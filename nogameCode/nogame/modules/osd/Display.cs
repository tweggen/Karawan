using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.draw.systems;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using static engine.Logger;

namespace nogame.modules.osd;

/**
 * Implement the actual OSD display.
 * The OSD pixel framebuffer has a logical resolution of 1365*768 , and a 3d size
 * of (4, 2.25).
 * We use it for buttons of height 1/18 height.
 * arranged in 17 rows.
 */
public class Display : engine.AController
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<RenderOSDSystem>() { ShallActivate = false },
        new MyModule<nogame.modules.osd.Camera>() { ShallActivate = false },

    };
    
    private engine.joyce.TransformApi _aTransform;

    private engine.draw.DoubleBufferedFramebuffer _framebuffer;
    private DefaultEcs.Entity _eFramebuffer;
    private engine.draw.Context _drawContext;
    private engine.joyce.Texture _textureFramebuffer;
    
    private uint _fbpos = 0;

    private readonly uint _width = 768;
    public uint Width => _width;
    
    private readonly uint _height = 768*9/16;
    public uint Height => _height;

    
    private engine.news.Event _osdClickEventFactory(
        DefaultEcs.Entity e, 
        engine.news.Event cev, 
        Vector2 v2RelPos)
    {
        var clickableEntities = _engine.GetEcsWorld().GetEntities()
            .With<Clickable>()
            .With<engine.draw.components.OSDText>()
            .AsEnumerable();
        
        Vector2 v2OsdPos = new(v2RelPos.X * _width, v2RelPos.Y * _height);

        Trace($"Handling relative click {v2OsdPos}");
        foreach (var eCand in clickableEntities)
        {
            ref var cOsdText = ref eCand.Get<engine.draw.components.OSDText>();

            ref var v2ScreenPos = ref cOsdText.ScreenPos;
            if (v2ScreenPos.X == -1000f)
            {
                continue;
            }

            if (v2OsdPos.X >= v2ScreenPos.X
                && v2OsdPos.Y >= v2ScreenPos.Y
                && v2OsdPos.X < (v2ScreenPos.X + cOsdText.Size.X)
                && v2OsdPos.Y < (v2ScreenPos.Y + cOsdText.Size.Y))
            {
                return eCand.Get<Clickable>().ClickEventFactory(e, cev, v2OsdPos);
            }
        }

        return null;
    }


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

    private float ButtonSize = 18f;
    private int ButtonsPerRow = 14;
    private int ButtonsPerColumn = (int) Single.Round(14f * 9f / 16f);
    private float ButtonOffsetX = 0.5f;
    private float ButtonOffsetY = 0.5f;

    
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


    private void _createButton(string tagButton, int x, int y, 
        Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event> func)
    {
        var eSettings = _engine.CreateEntity(tagButton);
        eSettings.Set(new Instance3(_createButtonMesh(tagButton)));
        _setButtonTransforms(eSettings, x, y);
        eSettings.Set(new Clickable { CameraLayer = 24, ClickEventFactory = func });
    }


    private void _setupOSD()
    {
        _drawContext = new engine.draw.Context();
        
        var buffer1 = new engine.ross.SkiaSharpFramebuffer("fbOsd_1", _width, _height);
        var buffer2 = new engine.ross.SkiaSharpFramebuffer("fbOsd_2", _width, _height);
        _framebuffer = new engine.draw.DoubleBufferedFramebuffer(buffer1, buffer2);

        {
            _eFramebuffer = _engine.CreateEntity("Framebuffer");
            
            engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
                "osd", new Vector2(2f, 2f*9f/16f));
            meshFramebuffer.UploadImmediately = true;
            _textureFramebuffer = new(_framebuffer.GetDisplayBuffer())
            {
                FilteringMode = engine.joyce.Texture.FilteringModes.Framebuffer
            };
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = _textureFramebuffer;
            materialFramebuffer.HasTransparency = true;

            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(materialFramebuffer, meshFramebuffer), 100f);
            _eFramebuffer.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            _eFramebuffer.Set(new engine.behave.components.Clickable()
            {
                ClickEventFactory = _osdClickEventFactory
            });
            _aTransform.SetTransforms(
                _eFramebuffer, true, 0x01000000,
                new Quaternion(0f,0f,0f,1f),
                new Vector3(0f, 0f, 1f));
        }

        if (GlobalSettings.Get("Android") == "true")
        {

            // _createButton("but_settings.png", ButtonsPerRow - 2, 0,
            //    (entity, ev, pos) => ev.IsPressed?new Event("nogame.modules.menu.toggleMenu", null):null);
            _createButton("but_left.png",  0, ButtonsPerColumn-2,
                (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "a"));
            _createButton("but_right.png", 1, ButtonsPerColumn-2,
                (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "d"));
            _createButton("but_accel.png", ButtonsPerRow-2, ButtonsPerColumn-2,
                (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "w"));
            _createButton("but_brake.png", ButtonsPerRow-3, ButtonsPerColumn-2,
                (entity, ev, pos) => new Event(ev.IsPressed?Event.INPUT_KEY_PRESSED:Event.INPUT_KEY_RELEASED, "s"));
        }
    }
    
    
    private uint _frameCounter = 0;
    private readonly uint _renderSubDiv = 2;
    private float _dtTotal = 0f;
    protected override void OnLogicalFrame(object? sender, float dt)
    {
        ++_frameCounter;
        _dtTotal += dt;
        if (_frameCounter < _renderSubDiv)
        {
            return;
        }

        var dtTotal = _dtTotal;
        _dtTotal = 0f;
        _frameCounter = 0;
        
        var renderOsdSystem = M<RenderOSDSystem>();
        if (_engine.Player.TryGet(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
        {
            renderOsdSystem.ReferencePosition = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
        }
        renderOsdSystem.Update(dtTotal);
        
        // Update texture after the render system has swapped buffers
        _textureFramebuffer.Framebuffer = _framebuffer.GetDisplayBuffer();
    }
    
    
    protected override void OnModuleDeactivate()
    {
        DeactivateMyModule<RenderOSDSystem>();
    }
    
    
    protected override void OnModuleActivate()
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();
        _setupOSD();
        M<RenderOSDSystem>().SetFramebuffer(_framebuffer);
        ActivateMyModule<RenderOSDSystem>();
        ActivateMyModule<nogame.modules.osd.Camera>();
    }
}