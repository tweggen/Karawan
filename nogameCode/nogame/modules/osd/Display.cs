using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave.components;
using engine.draw.systems;
using engine.joyce;
using static engine.Logger;

namespace nogame.modules.osd;

public class Display : engine.AModule
{
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
     * OSD rendering system
     */
    private engine.draw.systems.RenderOSDSystem _renderOSDSystem;
    
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
        Vector2 v2OSDPos = new(v2RelPos.X * _width, v2RelPos.Y * _height);
        //List<Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event>> listClickables = new();

        foreach (var eCand in clickableEntities)
        {
            var cOSDText = eCand.Get<engine.draw.components.OSDText>();

            Trace($"have clickable {cOSDText.Position} + {cOSDText.Size}: cOSDText");
        }

        Trace($"Handling relative click {v2OSDPos}");
        foreach (var eCand in clickableEntities)
        {
            var cOSDText = eCand.Get<engine.draw.components.OSDText>();

            if (v2OSDPos.X >= cOSDText.Position.X
                && v2OSDPos.Y >= cOSDText.Position.Y
                && v2OSDPos.X < (cOSDText.Position.X + cOSDText.Size.X)
                && v2OSDPos.Y < (cOSDText.Position.Y + cOSDText.Size.Y))
            {
                Trace($"matching {cOSDText.Position} + {cOSDText.Size}: cOSDText");
                //listClickables.Add(eCand.Get<Clickable>().ClickEventFactory);
                return eCand.Get<Clickable>().ClickEventFactory(e, cev, v2OSDPos);
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
                DoFilter = false
            };
            textureFramebuffer.DoFilter = false;
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
        _renderOSDSystem.Update(dtTotal);
    }

    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogical;
        _renderOSDSystem.Dispose();
        _renderOSDSystem = null;
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
 
        /*
         * local shortcuts.
         */
        _aTransform = I.Get<engine.joyce.TransformApi>();

        _renderOSDSystem = new RenderOSDSystem();

        _engine.AddModule(this);

        _engine.OnLogicalFrame += _onLogical;
        
        _setupOSD();
        _renderOSDSystem.SetFramebuffer(_framebuffer);
    }
}