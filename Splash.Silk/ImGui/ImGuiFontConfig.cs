using System;
using ImGuiNET;

namespace Splash.Silk.ImGui;

/**
 * Font selection for the ImGui controller.
 *
 * WP-5.3: inlined from Silk.NET.OpenGL.Extensions.ImGui, which was the ONLY type this
 * project used out of that package - one small struct holding a path, a size and an
 * optional glyph-range callback. Reproducing it here lets the package reference go, and
 * with it the last Silk dependency in the ImGui path.
 *
 * ImGui.NET itself stays. It is not a Silk package, it is already referenced directly,
 * and nothing about it is entangled with the GL binding.
 */
public readonly struct ImGuiFontConfig
{
    public ImGuiFontConfig(string fontPath, int fontSize, Func<ImGuiIOPtr, IntPtr>? getGlyphRange = null)
    {
        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        FontPath = fontPath ?? throw new ArgumentNullException(nameof(fontPath));
        FontSize = fontSize;
        GetGlyphRange = getGlyphRange;
    }

    public string FontPath { get; }
    public int FontSize { get; }

    /**
     * Returns a pointer to a glyph range owned by ImGui's font atlas. Null means the
     * default (Latin) range.
     */
    public Func<ImGuiIOPtr, IntPtr>? GetGlyphRange { get; }
}
