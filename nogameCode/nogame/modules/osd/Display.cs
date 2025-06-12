using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.controllers;
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
    
    private FingerStateHandler _fingerStateHandler;
    
    private engine.news.Event _osdClickEventFactory(
        DefaultEcs.Entity e, 
        engine.news.Event cev, 
        Vector2 v2RelPos)
    {
        Vector2 v2OsdPos = new(v2RelPos.X * _width, v2RelPos.Y * _height);
        // Trace($"Handling osd pos click {v2OsdPos}");


        switch (cev.Type)
        {
            case Event.INPUT_LOGICAL_PRESSED:

                var clickableEntities = _engine.GetEcsWorld().GetEntities()
                    .With<Clickable>()
                    .With<engine.draw.components.OSDText>()
                    .AsEnumerable();

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
                        Clickable cClickable = eCand.Get<Clickable>();
                        // TXWTODO: Figure out, which of therse positions should be carried on.
                        _fingerStateHandler.OnFingerPressed(cev, ev =>
                            new ClickableFingerState(cev.PhysicalPosition, eCand, cClickable, v2OsdPos));
                        return null;
                    }
                }

                break;
            case Event.INPUT_LOGICAL_MOVED:
                _fingerStateHandler.OnFingerMotion(cev);
                break;
            case Event.INPUT_LOGICAL_RELEASED:
                _fingerStateHandler.OnFingerReleased(cev);
                break;
        }

        return null;
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
                ClickEventFactory = _osdClickEventFactory,
                Flags = Clickable.ClickableFlags.AlsoOnRelease
            });
            _aTransform.SetTransforms(
                _eFramebuffer, true, 0x01000000,
                new Quaternion(0f,0f,0f,1f),
                new Vector3(0f, 0f, 1f));
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
        _fingerStateHandler = null;
        DeactivateMyModule<RenderOSDSystem>();
    }
    
    
    protected override void OnModuleActivate()
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();
        _fingerStateHandler = new();
        _setupOSD();
        M<RenderOSDSystem>().SetFramebuffer(_framebuffer);
        ActivateMyModule<RenderOSDSystem>();
        ActivateMyModule<nogame.modules.osd.Camera>();
    }
}