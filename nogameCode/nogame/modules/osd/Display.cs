﻿using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.draw.systems;
using engine.joyce;
using engine.joyce.components;
using static engine.Logger;

namespace nogame.modules.osd;

public class Display : engine.AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<RenderOSDSystem>() { ShallActivate = false } 
    };
    
    private engine.joyce.TransformApi _aTransform;

    /**
     * This is the standard framebuffer we draw into.
     */
    private engine.draw.IFramebuffer _framebuffer;
    
    /**
     * This is the 3d mesh textured with this framebuffer. 
     */
    DefaultEcs.Entity _eFramebuffer;

    /**
     * Default draw context.
     */
    private engine.draw.Context _drawContext;
    
    private uint _fbpos = 0;

    private readonly uint _width = 768;
    public uint Width
    {
        get => _width;
    }
    
    private readonly uint _height = 768*9/16;

    public uint Height
    {
        get => _height;
    }


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
        //List<Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event>> listClickables = new();

        #if false
        foreach (var eCand in clickableEntities)
        {
            ref var cOSDText = ref eCand.Get<engine.draw.components.OSDText>();

            if (cOSDText.ScreenPos.X == -1000f) continue;

            Trace($"have clickable {cOSDText.Position} + {cOSDText.Size}: cOSDText");
        }
        #endif

        Trace($"Handling relative click {v2OsdPos}");
        foreach (var eCand in clickableEntities)
        {
            ref var cOsdText = ref eCand.Get<engine.draw.components.OSDText>();

            ref var v2ScreenPos = ref cOsdText.ScreenPos;
            if (v2ScreenPos.X == -1000f)
            {
                continue;
            }

            //Trace($"testing {v2ScreenPos} + {cOsdText.Size}: cOSDText {cOsdText.Text}");
            if (v2OsdPos.X >= v2ScreenPos.X
                && v2OsdPos.Y >= v2ScreenPos.Y
                && v2OsdPos.X < (v2ScreenPos.X + cOsdText.Size.X)
                && v2OsdPos.Y < (v2ScreenPos.Y + cOsdText.Size.Y))
            {
                // Trace($"matching {v2ScreenPos} + {cOsdText.Size}: cOSDText");
                //listClickables.Add(eCand.Get<Clickable>().ClickEventFactory);
                return eCand.Get<Clickable>().ClickEventFactory(e, cev, v2OsdPos);
            }
        }

        return null;
    }


    private void _setupOSD()
    {
            
        _drawContext = new engine.draw.Context();
        _framebuffer = new engine.ross.SkiaSharpFramebuffer("fbOsd", _width, _height);

        {
            _eFramebuffer = _engine.CreateEntity("Framebuffer");
            
            // engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreateCubeMesh(4f);
            engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
                "osd", new Vector2(2f, 2f*9f/16f));
            meshFramebuffer.UploadImmediately = true;
            engine.joyce.Texture textureFramebuffer = new(_framebuffer)
            {
                FilteringMode = engine.joyce.Texture.FilteringModes.Framebuffer
            };
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
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
    }
    

    private uint _frameCounter = 0;
    private readonly uint _renderSubDiv = 2;
    private float _dtTotal = 0f;
    private void _onLogical(object? sender, float dt)
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
        if (_engine.TryGetPlayerEntity(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
        {
            renderOsdSystem.ReferencePosition = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
        }
        renderOsdSystem.Update(dtTotal);
    }

    
    public override void ModuleDeactivate()
    {
        DeactivateMyModule<RenderOSDSystem>();

        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogical;

        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
 
        /*
         * local shortcuts.
         */
        _aTransform = I.Get<engine.joyce.TransformApi>();


        _engine.AddModule(this);

        _engine.OnLogicalFrame += _onLogical;
        
        _setupOSD();
        M<RenderOSDSystem>().SetFramebuffer(_framebuffer);
        ActivateMyModule<RenderOSDSystem>();

    }
}