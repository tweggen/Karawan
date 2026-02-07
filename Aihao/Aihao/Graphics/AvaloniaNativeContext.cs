using System;
using Avalonia.OpenGL;
using Silk.NET.Core.Contexts;

namespace Aihao.Graphics;

/// <summary>
/// Bridges Avalonia's GlInterface.GetProcAddress to Silk.NET's INativeContext,
/// allowing creation of a Silk.NET GL instance from an Avalonia OpenGL context.
/// </summary>
public sealed class AvaloniaNativeContext : INativeContext
{
    private readonly GlInterface _glInterface;

    public AvaloniaNativeContext(GlInterface glInterface)
    {
        _glInterface = glInterface;
    }

    public nint GetProcAddress(string proc, int? slot = null)
    {
        return _glInterface.GetProcAddress(proc);
    }

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
    {
        addr = _glInterface.GetProcAddress(proc);
        return addr != nint.Zero;
    }

    public void Dispose()
    {
        // No-op: Avalonia owns the GL context lifetime
    }
}
