using System;
using Aihao.Services;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;

namespace Aihao.Controls;

/// <summary>
/// OpenGL preview control that renders L-System geometry via the Joyce engine.
/// </summary>
public class AvaloniaGlPreviewControl : OpenGlControlBase
{
    private bool _glInitialized;

    /// <summary>
    /// Number of follow-up frames to request after new content arrives.
    /// The engine's upload budget (1ms/frame) may require several frames
    /// to finish uploading materials (shader compile) and meshes before
    /// the first successful draw.
    /// </summary>
    private int _warmupFramesRemaining;

    /// <summary>
    /// Camera parameters set by the parent control.
    /// </summary>
    public float CameraDistance { get; set; } = 30f;
    public float CameraYaw { get; set; } = 45f;
    public float CameraPitch { get; set; } = 25f;

    /// <summary>
    /// Whether there is geometry ready to render.
    /// </summary>
    public bool HasContent
    {
        get => _hasContent;
        set
        {
            if (_hasContent != value)
            {
                _hasContent = value;
                if (value) _warmupFramesRemaining = 6;
            }
        }
    }
    private bool _hasContent;

    /// <summary>
    /// Raised when GL is ready and the engine preview service has been initialized.
    /// </summary>
    public event Action? GlReady;

    private bool _traceGL = false;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        if (_traceGL) System.Console.Error.WriteLine("[GlPreview] OnOpenGlInit called");

        var service = EnginePreviewService.Instance;
        if (!service.IsInitialized)
        {
            service.Initialize(gl);
        }

        _glInitialized = true;
        if (_traceGL) System.Console.Error.WriteLine($"[GlPreview] OnOpenGlInit done, IsInitialized={service.IsInitialized}, GlReady has {GlReady?.GetInvocationList()?.Length ?? 0} subscribers");
        GlReady?.Invoke();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_glInitialized || !_hasContent)
        {
            System.Console.Error.WriteLine($"[GlPreview] OnOpenGlRender skipped: _glInitialized={_glInitialized}, _hasContent={_hasContent}");
            return;
        }
        if (_traceGL) System.Console.Error.WriteLine($"[GlPreview] OnOpenGlRender: rendering frame");

        var bounds = Bounds;
        int width = Math.Max(1, (int)(bounds.Width * (VisualRoot?.RenderScaling ?? 1.0)));
        int height = Math.Max(1, (int)(bounds.Height * (VisualRoot?.RenderScaling ?? 1.0)));

        EnginePreviewService.Instance.RenderPreview(
            width, height, (uint)fb,
            CameraDistance, CameraYaw, CameraPitch);

        // Request follow-up frames so material/mesh uploads complete
        if (_warmupFramesRemaining > 0)
        {
            _warmupFramesRemaining--;
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
        _glInitialized = false;
    }
}
