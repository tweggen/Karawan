using System;
using Silk.NET.OpenGL;

namespace Splash.Silk;

/// <summary>
/// RAII-style GL state saver. Saves state on construction, restores on Dispose().
/// Modeled after the save/restore pattern in ImGui/Controller.cs.
/// </summary>
public sealed class GlStateSaver : IDisposable
{
    private readonly GL _gl;

    // Saved state
    private readonly int _lastActiveTexture;
    private readonly int _lastProgram;
    private readonly int _lastTexture;
    private readonly int _lastSampler;
    private readonly int _lastArrayBuffer;
    private readonly int _lastVertexArrayObject;
    private readonly int _lastFramebuffer;
    private readonly int[] _lastViewport = new int[4];
    private readonly int[] _lastScissorBox = new int[4];
    private readonly int _lastBlendSrcRgb;
    private readonly int _lastBlendDstRgb;
    private readonly int _lastBlendSrcAlpha;
    private readonly int _lastBlendDstAlpha;
    private readonly int _lastBlendEquationRgb;
    private readonly int _lastBlendEquationAlpha;
    private readonly bool _lastEnableBlend;
    private readonly bool _lastEnableCullFace;
    private readonly bool _lastEnableDepthTest;
    private readonly bool _lastEnableStencilTest;
    private readonly bool _lastEnableScissorTest;

    public GlStateSaver(GL gl)
    {
        _gl = gl;

        _gl.GetInteger(GLEnum.ActiveTexture, out _lastActiveTexture);
        _gl.ActiveTexture(GLEnum.Texture0);

        _gl.GetInteger(GLEnum.CurrentProgram, out _lastProgram);
        _gl.GetInteger(GLEnum.TextureBinding2D, out _lastTexture);
        _gl.GetInteger(GLEnum.SamplerBinding, out _lastSampler);
        _gl.GetInteger(GLEnum.ArrayBufferBinding, out _lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out _lastVertexArrayObject);
        _gl.GetInteger(GLEnum.FramebufferBinding, out _lastFramebuffer);

        Span<int> viewport = _lastViewport.AsSpan();
        _gl.GetInteger(GLEnum.Viewport, viewport);

        Span<int> scissor = _lastScissorBox.AsSpan();
        _gl.GetInteger(GLEnum.ScissorBox, scissor);

        _gl.GetInteger(GLEnum.BlendSrcRgb, out _lastBlendSrcRgb);
        _gl.GetInteger(GLEnum.BlendDstRgb, out _lastBlendDstRgb);
        _gl.GetInteger(GLEnum.BlendSrcAlpha, out _lastBlendSrcAlpha);
        _gl.GetInteger(GLEnum.BlendDstAlpha, out _lastBlendDstAlpha);
        _gl.GetInteger(GLEnum.BlendEquationRgb, out _lastBlendEquationRgb);
        _gl.GetInteger(GLEnum.BlendEquationAlpha, out _lastBlendEquationAlpha);

        _lastEnableBlend = _gl.IsEnabled(GLEnum.Blend);
        _lastEnableCullFace = _gl.IsEnabled(GLEnum.CullFace);
        _lastEnableDepthTest = _gl.IsEnabled(GLEnum.DepthTest);
        _lastEnableStencilTest = _gl.IsEnabled(GLEnum.StencilTest);
        _lastEnableScissorTest = _gl.IsEnabled(GLEnum.ScissorTest);
    }

    public void Dispose()
    {
        _gl.UseProgram((uint)_lastProgram);
        _gl.BindTexture(GLEnum.Texture2D, (uint)_lastTexture);
        _gl.BindSampler(0, (uint)_lastSampler);
        _gl.ActiveTexture((GLEnum)_lastActiveTexture);
        _gl.BindVertexArray((uint)_lastVertexArrayObject);
        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)_lastArrayBuffer);
        _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)_lastFramebuffer);

        _gl.BlendEquationSeparate(
            (GLEnum)_lastBlendEquationRgb, (GLEnum)_lastBlendEquationAlpha);
        _gl.BlendFuncSeparate(
            (GLEnum)_lastBlendSrcRgb, (GLEnum)_lastBlendDstRgb,
            (GLEnum)_lastBlendSrcAlpha, (GLEnum)_lastBlendDstAlpha);

        _setEnabled(GLEnum.Blend, _lastEnableBlend);
        _setEnabled(GLEnum.CullFace, _lastEnableCullFace);
        _setEnabled(GLEnum.DepthTest, _lastEnableDepthTest);
        _setEnabled(GLEnum.StencilTest, _lastEnableStencilTest);
        _setEnabled(GLEnum.ScissorTest, _lastEnableScissorTest);

        _gl.Viewport(
            _lastViewport[0], _lastViewport[1],
            (uint)_lastViewport[2], (uint)_lastViewport[3]);
        _gl.Scissor(
            _lastScissorBox[0], _lastScissorBox[1],
            (uint)_lastScissorBox[2], (uint)_lastScissorBox[3]);
    }

    private void _setEnabled(GLEnum cap, bool enabled)
    {
        if (enabled)
            _gl.Enable(cap);
        else
            _gl.Disable(cap);
    }
}
