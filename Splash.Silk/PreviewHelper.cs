using System;
using engine;
using Silk.NET.OpenGL;
using Splash;

namespace Splash.Silk;

/// <summary>
/// Renders preview geometry via the Joyce engine's Splash.Silk renderer.
/// GL initialization and render only — no engine bootstrap or game logic.
/// Uses the standard ECS pipeline (CollectRenderData → TryDequeueRenderFrame → RenderExternalFrame).
/// </summary>
public sealed class PreviewHelper
{
    private static readonly object _lo = new();
    private static PreviewHelper? _instance;

    private Platform? _platform;
    private GL? _gl;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    private PreviewHelper() { }

    public static PreviewHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lo)
                {
                    _instance ??= new PreviewHelper();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initialize the GL context. Must be called from the GL thread.
    /// The engine and platform must already be created before calling this.
    /// </summary>
    public void Initialize(Func<string, nint> getProcAddress, Platform platform)
    {
        if (_isInitialized) return;

        lock (_lo)
        {
            if (_isInitialized) return;

            _platform = platform;

            // Create the Silk.NET GL context from the getProcAddress delegate
            _gl = GL.GetApi(new DelegateProcContext(getProcAddress));

            // Initialize the platform's GL state
            _platform.SetExternalGL(_gl);

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Render via the standard ECS pipeline. Must be called from the GL thread.
    /// Camera and geometry entities must already exist in the ECS world.
    /// </summary>
    public void RenderPreview(int viewportWidth, int viewportHeight, uint targetFbo)
    {
        if (!_isInitialized || _platform == null) return;

        // Run the standard pipeline: systems process ECS entities and enqueue a RenderFrame
        _platform.CollectRenderData(null);

        // Non-blocking dequeue (frame was just enqueued by CollectRenderData)
        var renderFrame = I.Get<LogicalRenderer>().TryDequeueRenderFrame();
        if (renderFrame == null) return;

        _platform.RenderExternalFrame(in renderFrame, viewportWidth, viewportHeight,
            targetFbo, saveRestoreState: true);
    }

    /// <summary>
    /// Simple INativeContext implementation using a delegate.
    /// </summary>
    private sealed class DelegateProcContext : global::Silk.NET.Core.Contexts.INativeContext
    {
        private readonly Func<string, nint> _getProcAddress;

        public DelegateProcContext(Func<string, nint> getProcAddress)
        {
            _getProcAddress = getProcAddress;
        }

        public nint GetProcAddress(string proc, int? slot = null) => _getProcAddress(proc);

        public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
        {
            addr = _getProcAddress(proc);
            return addr != nint.Zero;
        }

        public void Dispose() { }
    }
}
