using System.Runtime.CompilerServices;

namespace engine;

/// <summary>
/// InterpolatedStringHandler for DebugFilter-gated Trace/Wonder/Warning calls.
/// The compiler transforms Trace(Dc.X, $"...") so that AppendLiteral/AppendFormatted
/// are only called when the category is enabled — string is NEVER built when disabled.
/// </summary>
[InterpolatedStringHandler]
public ref struct DebugInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;
    public readonly bool IsEnabled;

    public DebugInterpolatedStringHandler(
        int literalLength, int formattedCount,
        Dc category, out bool shouldAppend)
    {
        IsEnabled = DebugFilter.Is(category);
        shouldAppend = IsEnabled;
        _inner = IsEnabled
            ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
            : default;
    }

    public void AppendLiteral(string value)
    {
        if (IsEnabled) _inner.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value)
    {
        if (IsEnabled) _inner.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        if (IsEnabled) _inner.AppendFormatted(value, format);
    }

    public void AppendFormatted<T>(T value, int alignment)
    {
        if (IsEnabled) _inner.AppendFormatted(value, alignment);
    }

    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (IsEnabled) _inner.AppendFormatted(value, alignment, format);
    }

    public string ToStringAndClear() =>
        IsEnabled ? _inner.ToStringAndClear() : string.Empty;
}
