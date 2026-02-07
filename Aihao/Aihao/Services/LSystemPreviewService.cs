using System;
using System.Threading;
using System.Threading.Tasks;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Manages L-System generation on a background thread.
/// Delegates all engine/rendering work to PreviewHelper (which lives in Splash.Silk
/// to avoid type conflicts with the shared JoyceCode project).
/// </summary>
public sealed class LSystemPreviewService
{
    /// <summary>
    /// Generate an L-System on a background thread from a JSON definition.
    /// Returns true if geometry was produced and is ready for upload.
    /// </summary>
    public async Task<bool> GenerateAsync(string definitionJson, int iterations,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return PreviewHelper.Instance.GenerateLSystem(definitionJson, iterations);
        }, ct);
    }
}
