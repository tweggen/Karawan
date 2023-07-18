using System;
using System.Diagnostics;
using System.Numerics;
using BepuPhysics.CollisionDetection;
using engine.joyce.components;
using engine.transform.components;
using engine.world;
using static engine.Logger;

namespace engine.draw.systems;

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

    private DefaultEcs.Entity _eCamera;
    private bool _haveCamera;
    private Matrix4x4 _mCameraToWorld;
    private Camera3 _cCamera;
    private Transform3ToWorld _cCamTransform;
    private Matrix4x4 _mView;
    private Matrix4x4 _mProjection;

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
            
            if (entity.Has<Transform3ToWorld>())
            {
                if (!_haveCamera)
                {
                    continue;
                }

                
                var cEntityTransform = entity.Get<Transform3ToWorld>();
                //Matrix4x4 mModel = cEntityTransform.Matrix;
                Vector3 vModel = cEntityTransform.Matrix.Translation;
                // Vector4 vModel = new(0f, 35f, 0f, 1f);
                

                /*
                 * Render 3d label with position relative to the projected
                 * position.
                 *
                 * So first, transform the thing we render to world coordinates.
                 * Then use the world and the view matrices to convert to screen space.
                 */
                Vector4 vWorldPos4 = Vector4.Transform(vModel, _mView);
#if true
                if (vWorldPos4.Z*vWorldPos4.W > 0)
                {
                    // vWorldPos4.Z *= -1f;
                    isBehind = true;
                }
#endif
                Vector4 vScreenPos4 = Vector4.Transform(vWorldPos4, _mProjection);
                vScreenPos = new(
                    (vScreenPos4.X/vScreenPos4.W+1f) * (_vOSDViewSize.X/2f),
                    (-vScreenPos4.Y/vScreenPos4.W+1f) * (_vOSDViewSize.Y/2f));
            }
            else
            {
                vScreenPos = Vector2.Zero;
            }

            if (isBehind)
            {
                continue;
            }
            vScreenPos += osdText.Position;
            
            /*
             * Render standard 2d text.
             */
            Vector2 ul = vScreenPos;
            Vector2 lr = vScreenPos + osdText.Size - new Vector2(1f, 1f);
            if (!_clearWholeScreen)
            {
                _dc.ClearColor = osdText.FillColor;
                _framebuffer.ClearRectangle(_dc, ul, lr);
            }

            _dc.TextColor = osdText.TextColor;
            _dc.HAlign = osdText.HAlign;
            _framebuffer.DrawText(_dc, ul, lr, osdText.Text, (int)osdText.FontSize);

        }
    }
    
    
    private void _onCameraEntityChanged(object? _, DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_eCamera != entity)
            {
                _eCamera = entity;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            /*
             * We do not update the AL listener, instead we assume the listener to be
             * at the origin. Instead, we wait for everything else to update.
             */
        }
        
    }

    protected override void PreUpdate(double dt)
    {
        if (null == _framebuffer)
        {
            return;
        }

        _vOSDViewSize = new Vector2(_framebuffer.Width, _framebuffer.Height);
        _haveCamera = false;
        _mCameraToWorld = Matrix4x4.Identity;
        _cCamera = new();
        
        if (_eCamera.IsAlive)
        {
            if (_eCamera.Has<Transform3ToWorld>() && _eCamera.Has<Camera3>())
            {
                _cCamTransform = _eCamera.Get<Transform3ToWorld>();
                _cCamera = _eCamera.Get<Camera3>();
                _mCameraToWorld = _cCamTransform.Matrix;

                _cCamera.GetViewMatrix(out _mView, _mCameraToWorld);
                _cCamera.GetProjectionMatrix(out _mProjection, _vOSDViewSize);

                _haveCamera = true;
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
    
    public RenderOSDSystem(engine.Engine engine)
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _engine.OnCameraEntityChanged += _onCameraEntityChanged;
        _dc = new();
    }
}