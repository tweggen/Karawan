using System;
using System.Collections.Generic;
using System.Numerics;
using engine.draw.components;
using engine.editor.components;
using engine.joyce.components;
using static engine.Logger;

namespace engine.draw.systems;


internal class CameraEntry
{
    public Camera3 CCamera;
    public Transform3ToWorld CCamTransform;
    public Vector3 V3CamPosition;
    public Matrix4x4 MCameraToWorld;
    public Matrix4x4 MView;
    public Matrix4x4 MProjection;
}

/**
 * Straightforward implementation that renders every frame entirely without any optimization.
 */
[DefaultEcs.System.With(typeof(draw.components.OSDText))]
public class RenderOSDSystem : DefaultEcs.System.AEntitySetSystem<double>
{
    private object _lo = new();
    
    private engine.Engine _engine;
    private IFramebuffer? _framebuffer = null;

    private draw.Context _dc;

    private Vector2 _vOSDViewSize;

    private bool _clearWholeScreen = true;

    /**
     * This is a dictionary of the current cameras we have.
     * TXWTODO: Keep this camera set globally, check if faster.
     */
    private SortedDictionary<uint, CameraEntry> _mapCameras;

    protected override void Update(double dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        if (null == _framebuffer)
        {
            return;
        }

        foreach (var entity in entities)
        {
            components.OSDText cOsdText = entity.Get<components.OSDText>();

            Vector2 v2ScreenPos;
            bool isBehind = false;
            float distance;

            
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
                var cEntityTransform = entity.Get<Transform3ToWorld>();

                if (!_mapCameras.TryGetValue(cEntityTransform.CameraMask, out ce))
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

                distance = Vector3.Distance(ce.V3CamPosition, v3Model);
                if (distance > cOsdText.MaxDistance)
                {
                    continue;
                }

                
                Vector4 v4ScreenPos = Vector4.Transform(v4WorldPos, ce.MProjection);
                v2ScreenPos = glToSkia(v4ScreenPos);
            }
            else
            {
                v2ScreenPos = Vector2.Zero;
                distance = 0f;
            }

            if (isBehind)
            {
                continue;
            }
            v2ScreenPos += cOsdText.Position;
            
            
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
                if (true)
                {
                    float width = (float)cOsdText.GaugeValue / 4096f * cOsdText.Size.X;
                    _framebuffer.FillRectangle(_dc, ul, lr with { X = ul.X+width });
                }
            }
            
            /*
             * Render text part.
             */
            uint textColor;
            if ((cOsdText.OSDTextFlags & OSDText.ENABLE_DISTANCE_FADE) != 0)
            {
                float distFactor = 1f- distance / cOsdText.MaxDistance;
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
            _framebuffer.DrawText(_dc, ul, lr, cOsdText.Text, (int)cOsdText.FontSize);
            
            /*
             * If there is a highlight, render the hightlight.
             */
            bool shouldDraw = false;
            uint color = 0;
            
            if ((cOsdText.BorderColor & 0xff000000) != 0)
            {
                _dc.Color = color;
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

        _mapCameras = new();

        //TXWTODO: We need to have a per-camera resolution.
        _vOSDViewSize = new Vector2(_framebuffer.Width, _framebuffer.Height);

        IEnumerable<DefaultEcs.Entity> listCameras = 
            _engine.GetEcsWorld().GetEntities().With<Camera3>().With<Transform3ToWorld>()
            .AsEnumerable();
        foreach (var eCamera in listCameras)
        {
            if (eCamera.Has<Transform3ToWorld>() && eCamera.Has<Camera3>())
            {

                var cCamTransform = eCamera.Get<Transform3ToWorld>();
                if (!cCamTransform.IsVisible)
                {
                    continue;
                }
                CameraEntry ce = new();
                ce.CCamTransform = cCamTransform;
                ce.CCamera = eCamera.Get<Camera3>();
                

                ce.MCameraToWorld = ce.CCamTransform.Matrix;
                ce.V3CamPosition = ce.MCameraToWorld.Translation;

                ce.CCamera.GetViewMatrix(out ce.MView, ce.MCameraToWorld);
                ce.CCamera.GetProjectionMatrix(out ce.MProjection, _vOSDViewSize);
                _mapCameras[ce.CCamera.CameraMask] = ce;
            }
        }

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

    public void SetFramebuffer(IFramebuffer framebuffer)
    {
        _framebuffer = framebuffer;
        if (null == _framebuffer)
        {
            return;
        }
        
        /*
         * Do an initial clear of the framebuffer,
         */
        _dc.FillColor = 0x00000000;
        _framebuffer.ClearRectangle( _dc,
            new Vector2(0, 0), 
            new Vector2(_framebuffer.Width-1f, _framebuffer.Height-1f));
        
    }
    
    public RenderOSDSystem()
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
        _dc = new();
    }
}