using System;
using System.Numerics;
using BepuPhysics.CollisionDetection;
using engine.world;

namespace engine.draw.systems;

/**
 * Straightforward implementation that renders every frame entirely without any optimization.
 */
[DefaultEcs.System.With(typeof(draw.components.OSDText))]
public class RenderOSDSystem : DefaultEcs.System.AEntitySetSystem<double>
{
    private engine.Engine _engine;
    private IFramebuffer? _framebuffer = null;

    private draw.Context _dc;

    protected override void Update(double dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        if (null == _framebuffer)
        {
            return;
        }
        
        foreach (var entity in entities)
        {
            components.OSDText osdText = entity.Get<components.OSDText>();

            Vector2 ul = osdText.Position;
            Vector2 lr = osdText.Position + osdText.Size - new Vector2(1f, 1f);
            _dc.ClearColor = osdText.FillColor;
            _framebuffer.ClearRectangle(_dc, ul, lr);
            // TXWTODO: We ignore the font size.
            _dc.TextColor = osdText.TextColor;
            _framebuffer.DrawText(_dc, ul, lr, osdText.Text, (int)osdText.FontSize);
        }
    }

    protected override void PreUpdate(double dt)
    {
        if (null == _framebuffer)
        {
            return;
        }
        _framebuffer.BeginModification();
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
        _dc = new();
    }
}