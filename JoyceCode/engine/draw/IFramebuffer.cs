using System;
using System.Numerics;

namespace engine.draw;

public interface IFramebuffer
{
    public uint Width { get; }
    public uint Height { get; }
    public uint Generation { get; }

    public void FillRectangle(Context context, in Vector2 ul, in Vector2 lr);
    public void DrawText(Context context, in Vector2 ul, in Vector2 lr, in string text);

    public void MarkDirty();

    public void GetMemory(out Span<byte> spanBytes);
}