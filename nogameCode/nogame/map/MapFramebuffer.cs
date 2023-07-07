using engine.draw;

namespace nogame.map;
using engine.ross;

public class MapFramebuffer
{
    private readonly object _lo = new();
    private bool _wasCreated = false;
    private SkiaSharpFramebuffer _framebuffer;
    public uint MapWidth = 1024;
    public uint MapHeight = 1024;

    public IFramebuffer Framebuffer
    {
        get
        {
            _create();
            return _framebuffer;
        }
    }

    private void _create()
    {
        lock (_lo)
        {
            if (_wasCreated)
            {
                return;
            }

            _framebuffer = new engine.ross.SkiaSharpFramebuffer("fbMap", MapWidth, MapHeight);

            /*
             * Render the actual map data.
             */
            engine.Implementations.Get<builtin.map.IMapProvider>().WorldMapCreateBitmap(_framebuffer);
            _wasCreated = true;
        }

    }
}