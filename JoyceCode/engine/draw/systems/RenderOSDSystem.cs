using System;
using System.Numerics;
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

        _dc.FillColor = 0x00000000;
        
        // TXWTODO: Avoid redrawing everything every frame.
        _framebuffer.ClearRectangle( _dc,
            new Vector2(0, 0), 
            new Vector2(_framebuffer.Width-1f, _framebuffer.Height-1f));
        
        foreach (var entity in entities)
        {
            components.OSDText osdText = entity.Get<components.OSDText>();
            _dc.TextColor = osdText.TextColor;
            
            // TXWTODO: We ignore the font size.
            _framebuffer.DrawText(_dc, 
                osdText.Position, 
                osdText.Position+osdText.Size-new Vector2(1f,1f),
                osdText.Text);
        }
    }

    protected override void PreUpdate(double dt)
    {
        if (null == _framebuffer)
        {
            return;
        }
    }

    public void SetFramebuffer(IFramebuffer framebuffer)
    {
        _framebuffer = framebuffer;
    }
    
    public RenderOSDSystem(engine.Engine engine)
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _dc = new();
    }
}