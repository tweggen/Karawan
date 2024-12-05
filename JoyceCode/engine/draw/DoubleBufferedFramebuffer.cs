using System;
using System.Numerics;

namespace engine.draw;

public class DoubleBufferedFramebuffer : IFramebuffer
{
    private readonly IFramebuffer[] _buffers = new IFramebuffer[2];
    private int _currentBuffer = 0;
    private bool _isModifying = false;

    public string Id => $"DoubleBuffer({_buffers[0].Id},{_buffers[1].Id})";
    public uint Width => _buffers[0].Width;
    public uint Height => _buffers[0].Height;
    public uint Generation => GetRenderBuffer().Generation;

    public DoubleBufferedFramebuffer(IFramebuffer buffer1, IFramebuffer buffer2)
    {
        _buffers[0] = buffer1;
        _buffers[1] = buffer2;
    }

    private IFramebuffer GetRenderBuffer() => _buffers[_currentBuffer];
    public IFramebuffer GetDisplayBuffer() => _buffers[1 - _currentBuffer];

    public void GetModified(out Vector2 ul, out Vector2 lr) => 
        GetDisplayBuffer().GetModified(out ul, out lr);

    public void SetConsumed() => 
        GetDisplayBuffer().SetConsumed();

    public void BeginModification()
    {
        if (_isModifying) return;
        GetRenderBuffer().BeginModification();
        _isModifying = true;
    }

    public void EndModification()
    {
        if (!_isModifying) return;
        GetRenderBuffer().EndModification();
        _isModifying = false;
        _currentBuffer = 1 - _currentBuffer;
    }

    public void PushClipping(Vector2 ul, Vector2 lr) => 
        GetRenderBuffer().PushClipping(ul, lr);

    public void PopClipping() => 
        GetRenderBuffer().PopClipping();

    public void DrawRectangle(Context context, Vector2 ul, Vector2 lr) => 
        GetRenderBuffer().DrawRectangle(context, ul, lr);

    public void FillRectangle(Context context, Vector2 ul, Vector2 lr) => 
        GetRenderBuffer().FillRectangle(context, ul, lr);

    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr) => 
        GetRenderBuffer().ClearRectangle(context, ul, lr);

    public void FillPoly(Context context, in Vector2[] polyPoints) => 
        GetRenderBuffer().FillPoly(context, polyPoints);

    public void DrawPoly(Context context, in Vector2[] polyPoints) => 
        GetRenderBuffer().DrawPoly(context, polyPoints);

    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text, uint fontSize) => 
        GetRenderBuffer().DrawText(context, ul, lr, text, fontSize);

    public void TextExtent(Context context, out Vector2 ul, out Vector2 size, out float ascent, 
        out float descent, string text, uint fontSize, bool includeWhiteSpaces = false) => 
        GetRenderBuffer().TextExtent(context, out ul, out size, out ascent, out descent, text, fontSize, includeWhiteSpaces);

    public void GetMemory(out Span<byte> spanBytes) => 
        GetDisplayBuffer().GetMemory(out spanBytes);
}