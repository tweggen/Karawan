using System;
using System.Numerics;
using BepuPhysics.CollisionDetection;
using engine.joyce.components;
using engine.transform.components;
using engine.world;

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

    private DefaultEcs.Entity _eCamera;
    private Vector2 _vOSDViewSize = new (512,256);

    private bool _clearWholeScreen = true;

    protected override void Update(double dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        if (null == _framebuffer)
        {
            return;
        }

        bool haveCamera = false;
        Matrix4x4 mCameraToWorld = new();
        joyce.components.Camera3 cCamera = new();
        transform.components.Transform3ToWorld cCamTransform;
        
        if (_eCamera.IsAlive)
        {
            if (_eCamera.Has<Transform3ToWorld>() && _eCamera.Has<Camera3>())
            {
                cCamTransform = _eCamera.Get<Transform3ToWorld>();
                cCamera = _eCamera.Get<Camera3>();
                mCameraToWorld = cCamTransform.Matrix;
                haveCamera = true;
            }
        }
        
        foreach (var entity in entities)
        {
            components.OSDText osdText = entity.Get<components.OSDText>();

            Vector2 vScreenPos;
            
            if (entity.Has<Transform3ToWorld>())
            {
                if (!haveCamera)
                {
                    continue;
                }

                
                var cEntityTransform = entity.Get<Transform3ToWorld>();
                Matrix4x4 mModel = cEntityTransform.Matrix;
                
                /*
                 * Render 3d label with position relative to the projected
                 * position.
                 *
                 * So first, transform the thing we render to world coordinates.
                 * Then use the world and the view matrices to convert to screen space.
                 */
                cCamera.GetViewMatrix(out Matrix4x4 mView, mCameraToWorld);
                cCamera.GetProjectionMatrix(out Matrix4x4 mProjection, _vOSDViewSize);
                Vector3 vScreenPos3 = Vector3.Transform(Vector3.Zero, mModel * mView * mProjection);
                vScreenPos = new(vScreenPos3.X + _vOSDViewSize.X/2f, -vScreenPos3.Y + _vOSDViewSize.Y/2f);
            }
            else
            {
                vScreenPos = Vector2.Zero;
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
        _framebuffer.BeginModification();

        if (_clearWholeScreen)
        {
            _dc.FillColor = 0x00000000;
            _framebuffer.ClearRectangle(_dc,
                new Vector2(0, 0),
                new Vector2(_framebuffer.Width - 1f, _framebuffer.Height - 1f));
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