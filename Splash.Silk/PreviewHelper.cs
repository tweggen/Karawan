using System;
using engine;
using Karawan.Graphics.OpenGL;
using Splash;
using static engine.Logger;

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
    /// Detect the graphics API and version behind <paramref name="getProcAddress"/> and record
    /// it in GlobalSettings as platform.threeD.API / platform.threeD.API.version. Must be
    /// called from the GL thread, before Initialize.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in the caller so that hosts embedding the renderer (Aihao) never
    /// have to name a Silk.NET type just to ask what context they were handed - see WP-0.2.
    /// The delegate is the whole contract: no GL object crosses the boundary.
    /// </remarks>
    public static void DetectAndSetGlVersion(Func<string, nint> getProcAddress)
    {
        // The generated binding takes the resolver directly - no INativeContext shim, which
        // is the only reason DelegateProcContext existed (WP-5.2).
        var gl = GL.GetApi(getProcAddress);
        string versionStr = gl.GetStringS(StringName.Version) ?? "";
        Trace($"GL version string: \"{versionStr}\"");

        string api;
        string version;

        if (versionStr.StartsWith("OpenGL ES", StringComparison.OrdinalIgnoreCase))
        {
            // e.g. "OpenGL ES 3.0" or "OpenGL ES 3.1 ..."
            api = "OpenGLES";
            var parts = versionStr.Split(' ');
            // parts[2] should be "3.0" or "3.1"
            var verParts = (parts.Length >= 3 ? parts[2] : "3.0").Split('.');
            int major = int.TryParse(verParts[0], out var m) ? m : 3;
            int minor = int.TryParse(verParts.Length > 1 ? verParts[1] : "0", out var n) ? n : 0;
            version = $"{major}{minor}0";
        }
        else
        {
            // e.g. "4.6.0 NVIDIA 546.33" or "3.3 Mesa 23.1"
            api = "OpenGL";
            var verPart = versionStr.Split(' ')[0]; // "4.6.0" or "3.3"
            var parts = verPart.Split('.');
            int major = int.TryParse(parts[0], out var m) ? m : 3;
            int minor = int.TryParse(parts.Length > 1 ? parts[1] : "3", out var n) ? n : 3;
            version = $"{major}{minor}0";
        }

        Trace($"Detected API={api}, version={version}");
        engine.GlobalSettings.Set("platform.threeD.API", api);
        engine.GlobalSettings.Set("platform.threeD.API.version", version);
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
            _gl = GL.GetApi(getProcAddress);

            // Initialize the platform's GL state
            _platform.SetExternalGL(_gl);

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Render via the standard ECS pipeline. Must be called from the GL thread.
    /// The logical thread produces RenderFrames via CollectRenderData; we just dequeue and render.
    /// </summary>
    public void RenderPreview(int viewportWidth, int viewportHeight, uint targetFbo)
    {
        if (!_isInitialized || _platform == null) return;

        // Non-blocking dequeue — logical thread produces frames
        var renderFrame = I.Get<Splash.LogicalRenderer>().TryDequeueRenderFrame();
        if (renderFrame == null)
        {
            return;
        }

        int nParts = renderFrame.RenderParts.Count;
        int nEntities = renderFrame.FrameStats.NEntities;
        // System.Console.Error.WriteLine(
        //     $"[PreviewHelper] Frame #{renderFrame.FrameNumber}: {nParts} parts, {nEntities} entities, viewport={viewportWidth}x{viewportHeight}, fbo={targetFbo}");

        _platform.RenderExternalFrame(in renderFrame, viewportWidth, viewportHeight,
            targetFbo, saveRestoreState: true);
    }

}
