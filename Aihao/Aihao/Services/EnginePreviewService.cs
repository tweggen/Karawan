using Avalonia.OpenGL;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Thin wrapper around PreviewHelper that manages initialization from Avalonia's GL context.
/// Set ResourcePath before the GL control initializes (e.g. when opening a project).
/// </summary>
public sealed class EnginePreviewService
{
    private static readonly object _lo = new();
    private static EnginePreviewService? _instance;

    public bool IsInitialized => PreviewHelper.Instance.IsInitialized;

    /// <summary>
    /// The project's resource directory (e.g. AihaoProject.ProjectDirectory).
    /// Must be set before Initialize() is called.
    /// </summary>
    public string? ResourcePath { get; set; }

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
        if (string.IsNullOrEmpty(ResourcePath)) return;

        PreviewHelper.Instance.Initialize(glInterface.GetProcAddress, ResourcePath!);
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
