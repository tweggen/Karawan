using System;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using Silk.NET.OpenGL;
using Splash;
using Splash.components;

namespace Splash.Silk;

/// <summary>
/// Renders preview geometry via the Joyce engine's Splash.Silk renderer.
/// GL initialization and render only — no engine bootstrap or game logic.
/// </summary>
public sealed class PreviewHelper
{
    private static readonly object _lo = new();
    private static PreviewHelper? _instance;

    private Platform? _platform;
    private GL? _gl;
    private bool _isInitialized;

    private PfInstance? _currentPfInstance;
    private object? _pendingInstanceDesc;

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
    /// Thread-safe setter for pending mesh geometry.
    /// The mesh will be uploaded on the next RenderPreview call (GL thread).
    /// </summary>
    public void SetInstanceDesc(InstanceDesc id)
    {
        lock (_lo) { _pendingInstanceDesc = id; }
    }

    /// <summary>
    /// Clear any pending or current geometry.
    /// </summary>
    public void ClearInstanceDesc()
    {
        lock (_lo)
        {
            _pendingInstanceDesc = null;
            _currentPfInstance = null;
        }
    }

    /// <summary>
    /// Render the current geometry to the given FBO. Must be called from the GL thread.
    /// </summary>
    public void RenderPreview(int viewportWidth, int viewportHeight, uint targetFbo,
        float cameraDistance, float cameraYaw, float cameraPitch)
    {
        if (!_isInitialized || _platform == null) return;

        // Upload pending mesh if available
        lock (_lo)
        {
            if (_pendingInstanceDesc is InstanceDesc id)
            {
                _currentPfInstance = _platform.InstanceManager.CreatePfInstance(id);
                _pendingInstanceDesc = null;
            }
        }

        if (_currentPfInstance == null) return;

        var threeD = I.Get<IThreeD>();

        // Build camera transform from orbit parameters
        var cameraTransform = BuildCameraTransform(cameraDistance, cameraYaw, cameraPitch);

        var camera3 = new Camera3
        {
            Angle = 60f,
            NearFrustum = 0.1f,
            FarFrustum = 1000f,
            UL = Vector2.Zero,
            LR = Vector2.One,
            CameraMask = 0xffffffff
        };

        var frameStats = new FrameStats();
        var cameraOutput = new CameraOutput(null, in threeD, in cameraTransform, in camera3, frameStats);

        var pfInst = _currentPfInstance.Value;
        var identity = Matrix4x4.Identity;
        var defaultAnimState = new GPUAnimationState();
        cameraOutput.AppendInstance(in pfInst, in identity, in defaultAnimState);
        cameraOutput.ComputeAfterAppend();

        var renderFrame = new RenderFrame
        {
            FrameNumber = 0,
            FrameStats = frameStats
        };

        // Collect lights from ECS entities (matches how LogicalRenderer works)
        renderFrame.LightCollector.CollectLights();

        var renderPart = new RenderPart
        {
            CameraOutput = cameraOutput,
            PfRenderbuffer = new PfRenderbuffer()
        };
        renderFrame.RenderParts.Add(renderPart);

        _platform.RenderExternalFrame(in renderFrame, viewportWidth, viewportHeight, targetFbo, saveRestoreState: true);
    }

    private static Matrix4x4 BuildCameraTransform(float distance, float yaw, float pitch)
    {
        float yawRad = yaw * MathF.PI / 180f;
        float pitchRad = pitch * MathF.PI / 180f;

        float cosP = MathF.Cos(pitchRad);
        float sinP = MathF.Sin(pitchRad);
        float cosY = MathF.Cos(yawRad);
        float sinY = MathF.Sin(yawRad);

        var cameraPos = new Vector3(
            distance * cosP * sinY,
            distance * sinP,
            distance * cosP * cosY);

        var target = Vector3.Zero;
        var up = Vector3.UnitY;

        var forward = Vector3.Normalize(target - cameraPos);
        var right = Vector3.Normalize(Vector3.Cross(forward, up));
        var cameraUp = Vector3.Cross(right, forward);

        var m = Matrix4x4.Identity;
        m.M11 = right.X; m.M12 = right.Y; m.M13 = right.Z;
        m.M21 = cameraUp.X; m.M22 = cameraUp.Y; m.M23 = cameraUp.Z;
        m.M31 = -forward.X; m.M32 = -forward.Y; m.M33 = -forward.Z;
        m.M41 = cameraPos.X; m.M42 = cameraPos.Y; m.M43 = cameraPos.Z;

        return m;
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
