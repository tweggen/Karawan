using System;
using System.Numerics;

namespace engine.draw;

public interface IFramebuffer
{
    public uint Width { get; }
    public uint Height { get; }
    public uint Generation { get; }

    public void GetModified(out Vector2 ul, out Vector2 lr);
    public void SetConsumed();

    public void BeginModification();
    public void EndModification();
    public void FillRectangle(Context context, Vector2 ul, Vector2 lr);
    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr);
        
    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text, int fontSize);

    
    public void GetMemory(out Span<byte> spanBytes);
}