using System;
using System.Collections.Generic;
using System.Numerics;
using BepuUtilities;
using engine.draw.components;
using engine.editor.components;
using engine.joyce.components;
using engine.world;
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

#if false
    private DefaultEcs.Entity _eCamera;
    private bool _haveCamera;
    private Matrix4x4 _mCameraToWorld;
    private Camera3 _cCamera;
    private Transform3ToWorld _cCamTransform;
    private Vector3 _vCamPosition;
    private Matrix4x4 _mView;
    private Matrix4x4 _mProjection;
#endif

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
            components.OSDText osdText = entity.Get<components.OSDText>();

            Vector2 vScreenPos;
            bool isBehind = false;
            float distance;
            
            if (entity.Has<Transform3ToWorld>())
            {
                var cEntityTransform = entity.Get<Transform3ToWorld>();

                CameraEntry ce = null;
                if (!_mapCameras.TryGetValue(cEntityTransform.CameraMask, out ce))
                {
                    continue;
                }

                Vector3 vModel = cEntityTransform.Matrix.Translation;

                /*
                 * Render 3d label with position relative to the projected
                 * position.
                 *
                 * So first, transform the thing we render to world coordinates.
                 * Then use the world and the view matrices to convert to screen space.
                 */
                Vector4 vWorldPos4 = Vector4.Transform(vModel, ce.MView);
#if true
                if (vWorldPos4.Z*vWorldPos4.W > 0)
                {
                    // vWorldPos4.Z *= -1f;
                    isBehind = true;
                }
#endif

                distance = Vector3.Distance(ce.V3CamPosition, vModel);
                if (distance > osdText.MaxDistance)
                {
                    continue;
                }

                
                Vector4 vScreenPos4 = Vector4.Transform(vWorldPos4, ce.MProjection);
                vScreenPos = new(
                    (vScreenPos4.X/vScreenPos4.W+1f) * (_vOSDViewSize.X/2f),
                    (-vScreenPos4.Y/vScreenPos4.W+1f) * (_vOSDViewSize.Y/2f));
            }
            else
            {
                vScreenPos = Vector2.Zero;
                distance = 0f;
            }

            if (isBehind)
            {
                continue;
            }
            vScreenPos += osdText.Position;
            
            /*
             * Render standard 2d entities.
             */
            Vector2 ul = vScreenPos;
            Vector2 lr = vScreenPos + osdText.Size - new Vector2(1f, 1f);
            
            /*
             * If we didn't clear the screen before, clear the component's rectangle.
             */
            if (!_clearWholeScreen)
            {
                _dc.ClearColor = osdText.FillColor;
                _framebuffer.ClearRectangle(_dc, ul, lr);
            }

            /*
             * Render text part.
             */
            uint textColor;
            if ((osdText.OSDTextFlags & OSDText.ENABLE_DISTANCE_FADE) != 0)
            {
                float distFactor = 1f- distance / osdText.MaxDistance;
                byte distAlpha = (byte)(distFactor * (osdText.TextColor>>24));
                textColor = (osdText.TextColor & 0xffffff) | (uint)(distAlpha << 24);
            }
            else
            {
                textColor = osdText.TextColor;
            }
            
            _dc.TextColor = textColor;
            _dc.HAlign = osdText.HAlign;
            _dc.VAlign = osdText.VAlign;
            _framebuffer.DrawText(_dc, ul, lr, osdText.Text, (int)osdText.FontSize);
            
            /*
             * If there is a highlight, render the hightlight.
             */
            bool shouldDraw = false;
            uint color = 0;

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