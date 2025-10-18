using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using engine.draw.components;
using engine.editor.components;
using engine.joyce.components;
using static engine.Logger;

namespace engine.draw.systems;

[DefaultEcs.System.With(typeof(draw.components.OSDText))]
public class RenderOSDSystem : DefaultEcs.System.AEntitySetSystem<double>, IModule
{
    private object _lo = new();
    protected ModuleTracker _moduleTracker;
    private engine.Engine _engine;
    private DoubleBufferedFramebuffer? _framebuffer = null;
    private draw.Context _dc;
    private Vector2 _vOSDViewSize;
    private bool _clearWholeScreen = true;
    private builtin.tools.CameraWatcher _cameraWatcher;
    public Vector3 ReferencePosition = Vector3.Zero;

    protected override void Update(double dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        if (null == _framebuffer)
        {
            return;
        }

        foreach (var entity in entities)
        {
            ref components.OSDText cOsdText = ref entity.Get<components.OSDText>();
            cOsdText.ScreenPos = new(-1000f, -1000f);

            Vector2 v2ScreenPos;
            bool isBehind = false;
            float distanceSquared;
            float maxDistanceSquared = cOsdText.MaxDistance * cOsdText.MaxDistance;
            
            CameraEntry ce = null;

            var glToSkia = (in Vector4 v4ViewPos) =>
            {
                ce.CCamera.ToScreenPosition(v4ViewPos, out var v2ScreenPosWindowed);
                return new Vector2(
                    (v2ScreenPosWindowed.X + 1f) * (_vOSDViewSize.X / 2f),
                    (v2ScreenPosWindowed.Y + 1f) * (_vOSDViewSize.Y / 2f)
                );
            };

            if (entity.Has<Transform3ToWorld>())
            {
                ref var cEntityTransform = ref entity.Get<Transform3ToWorld>();

                ce = _cameraWatcher.GetCameraEntry(cEntityTransform.CameraMask & cOsdText.CameraMask); 
                if (null == ce)
                {
                    continue;
                }

                Vector3 v3Model = cEntityTransform.Matrix.Translation;

                /*
                 * Render 3d label with position relative to the projected
                 * position.
                 *
                 * So first, transform the thing we render to world coordinates.
                 * Then use the world and the view matrices to convert to screen space.
                 */
                Vector4 v4WorldPos = Vector4.Transform(v3Model, ce.MView);
                if (v4WorldPos.Z*v4WorldPos.W > 0)
                {
                    isBehind = true;
                }

                distanceSquared = Vector3.DistanceSquared(ReferencePosition, v3Model);
                if (distanceSquared > maxDistanceSquared)
                {
                    continue;
                }

                
                Vector4 v4ScreenPos = Vector4.Transform(v4WorldPos, ce.MProjection);
                v2ScreenPos = glToSkia(v4ScreenPos);
            }
            else
            {
                v2ScreenPos = Vector2.Zero;
                distanceSquared = 0f;
            }

            if (isBehind)
            {
                continue;
            }
            v2ScreenPos += cOsdText.Position;
            cOsdText.ScreenPos = v2ScreenPos;
            
            
            /*
             * Setup clipping if we are associated with a camera.
             */
            if (ce != null)
            {
                var v2UL = glToSkia(new Vector4(-1f, 1f, 0f, 1f));
                var v2LR = glToSkia(new Vector4(1f, -1f, 0f, 1f));
                _framebuffer.PushClipping(v2UL, v2LR);
            }

            /*
             * Render standard 2d entities.
             */
            Vector2 ul = v2ScreenPos;
            Vector2 lr = v2ScreenPos + cOsdText.Size - new Vector2(1f, 1f);
            
            /*
             * If we didn't clear the screen before, clear the component's rectangle.
             */
            if ((cOsdText.FillColor & 0xff000000) != 0)
            {
                _dc.ClearColor = cOsdText.FillColor;
                _framebuffer.ClearRectangle(_dc, ul, lr);
            }
            
            if (cOsdText.GaugeValue != 0 && (cOsdText.GaugeColor & 0xff000000) != 0)
            {
                _dc.FillColor = cOsdText.GaugeColor;

                switch (cOsdText.OSDTextFlags & (ushort)OSDText.GAUGE_TYPE_MASK)
                {
                    case OSDText.GAUGE_TYPE_STANDARD:
                    {
                        float width = (float)cOsdText.GaugeValue / 4096f * cOsdText.Size.X;
                        _framebuffer.FillRectangle(_dc, ul, lr with { X = ul.X + width });
                        break;
                    }
                    case OSDText.GAUGE_TYPE_INSERT:
                    {
                        _framebuffer.TextExtent(
                            _dc, 
                            out var ulText, 
                            out var sizeText, 
                            out var ascent,
                            out var descent,
                            cOsdText.Text.Substring(0, cOsdText.GaugeValue), 
                            cOsdText.FontSize,
                            true
                        );
                        
                        float pos = sizeText.X + ulText.X;
                        Vector2 ulRect = ul + new Vector2(pos, 0);
                        Vector2 lrRect = ul + new Vector2(pos, descent-ascent+1);
                        _framebuffer.FillRectangle(_dc, ulRect, lrRect); 
                        break;
                    }
                }
                if (true)
                {
                }
            }
            
            /*
             * Render text part.
             */
            uint textColor;
            if ((cOsdText.OSDTextFlags & OSDText.ENABLE_DISTANCE_FADE) != 0)
            {
                float distFactor = 1f- distanceSquared / maxDistanceSquared;
                byte distAlpha = (byte)(distFactor * (cOsdText.TextColor>>24));
                textColor = (cOsdText.TextColor & 0xffffff) | (uint)(distAlpha << 24);
            }
            else
            {
                textColor = cOsdText.TextColor;
            }
            
            _dc.TextColor = textColor;
            _dc.HAlign = cOsdText.HAlign;
            _dc.VAlign = cOsdText.VAlign;
            _framebuffer.DrawText(_dc, ul, lr, cOsdText.Text, cOsdText.FontSize);
            
            /*
             * If there is a highlight, render the hightlight.
             */
            bool shouldDraw = false;
            uint color = 0;
            
            if ((cOsdText.BorderColor & 0xff000000) != 0)
            {
                _dc.Color = cOsdText.BorderColor;
                _framebuffer.DrawRectangle(_dc, ul, lr);
            }
    
            if (entity.Has<Highlight>())
            {
                ref var cHighlight = ref entity.Get<Highlight>();
                if (0 != (cHighlight.Flags & (byte)Highlight.StateFlags.IsSelected))
                {
                    shouldDraw = true;
                    color = cHighlight.Color;
                }
            }

            if (shouldDraw)
            {
                _dc.Color = color;
                _framebuffer.DrawRectangle(_dc, ul, lr);
            }

            if (ce != null)
            {
                _framebuffer.PopClipping();
            }
        }
    }
    

    protected override void PreUpdate(double dt)
    {
        if (null == _framebuffer)
        {
            return;
        }

        _cameraWatcher = I.Get<CameraWatcher>();
        _vOSDViewSize = new Vector2(_framebuffer.Width, _framebuffer.Height);
        _framebuffer.BeginModification();

        if (_clearWholeScreen)
        {
            _dc.ClearColor = 0x00000000;
            _framebuffer.ClearRectangle(_dc,
                new Vector2(0, 0),
                new Vector2(_framebuffer.Width, _framebuffer.Height));
        }
    }

    protected override void PostUpdate(double dt)
    {
        if (null == _framebuffer)
        {
            return;
        }
        _framebuffer.EndModification();
    }

    public void SetFramebuffer(DoubleBufferedFramebuffer framebuffer)
    {
        _framebuffer = framebuffer;
        
        if (framebuffer == null)
        {
            return;
        }
        
        // Initial clear of both buffers
        _dc.FillColor = 0x00000000;
        _framebuffer.BeginModification();
        _framebuffer.ClearRectangle(_dc,
            new Vector2(0, 0), 
            new Vector2(_framebuffer.Width-1f, _framebuffer.Height-1f));
        _framebuffer.EndModification();
        
        _framebuffer.BeginModification();
        _framebuffer.ClearRectangle(_dc,
            new Vector2(0, 0), 
            new Vector2(_framebuffer.Width-1f, _framebuffer.Height-1f));
        _framebuffer.EndModification();
    }

    public T M<T>() where T : class => _moduleTracker.M<T>();

    public virtual IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<CameraWatcher>()
    };
    
    public virtual void ModuleDeactivate() => _moduleTracker.ModuleDeactivate();
    
    public virtual void ModuleActivate() 
    {
        _moduleTracker.ModuleDependencies = ModuleDepends();
        _moduleTracker.ModuleActivate();  
    } 
    
    public virtual bool IsModuleActive() => _moduleTracker._isActivated;
    
    public override void Dispose()
    {
        _moduleTracker.Dispose();
        base.Dispose();
    }

    public RenderOSDSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
        _moduleTracker = new() { Module = this };
        _dc = new();
    }
}