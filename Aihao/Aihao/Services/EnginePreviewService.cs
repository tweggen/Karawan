using Avalonia.OpenGL;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Thin wrapper around PreviewHelper that manages initialization from Avalonia's GL context.
/// </summary>
public sealed class EnginePreviewService
{
    private static readonly object _lo = new();
    private static EnginePreviewService? _instance;

    public bool IsInitialized => PreviewHelper.Instance.IsInitialized;

    private EnginePreviewService() { }

    public static EnginePreviewService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lo)
                {
                    _instance ??= new EnginePreviewService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initialize the headless engine. Must be called from the GL thread.
    /// </summary>
    public void Initialize(GlInterface glInterface)
    {
        if (PreviewHelper.Instance.IsInitialized) return;
        PreviewHelper.Instance.Initialize(glInterface.GetProcAddress);
    }

    /// <summary>
    /// Render the current preview geometry. Must be called from the GL thread.
    /// </summary>
    public void RenderPreview(int width, int height, uint targetFbo,
        float cameraDistance, float cameraYaw, float cameraPitch)
    {
        PreviewHelper.Instance.RenderPreview(width, height, targetFbo,
            cameraDistance, cameraYaw, cameraPitch);
    }
}
