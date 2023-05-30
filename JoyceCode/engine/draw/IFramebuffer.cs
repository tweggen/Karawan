using System.Numerics;

namespace engine.draw;

public interface IFramebuffer
{
    public void FillRectangle(in Context context, in Vector2 ul, in Vector2 lr);
    public void DrawText(in Context context, in Vector2 ul, in Vector2 lr, in string text);

    public void MarkDirty();
}