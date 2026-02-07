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
/// Helper that encapsulates headless engine + preview rendering.
/// Lives in Splash.Silk to avoid type conflicts when called from projects
/// that also include JoyceCode as a shared project.
/// </summary>
public sealed class PreviewHelper
{
    private static readonly object _lo = new();
    private static PreviewHelper? _instance;

    private engine.Engine? _engine;
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
    /// Initialize the headless engine. Must be called from the GL thread.
    /// The getProcAddress delegate bridges the Avalonia GL context.
    /// </summary>
    public void Initialize(Func<string, nint> getProcAddress)
    {
        if (_isInitialized) return;

        lock (_lo)
        {
            if (_isInitialized) return;

            // Register TextureCatalogue before engine creation
            I.Register<TextureCatalogue>(() => new TextureCatalogue());

            // Create the headless engine + platform
            _engine = Platform.EasyCreateHeadless(Array.Empty<string>(), out _platform);

            // Register a synthetic "rgba" atlas entry so FindColorTexture works
            var tc = I.Get<TextureCatalogue>();
            tc.AddAtlasEntry("rgba", "rgba", Vector2.Zero, Vector2.One, 64, 64, false);

            // Create the Silk.NET GL context from the getProcAddress delegate
            _gl = GL.GetApi(new DelegateProcContext(getProcAddress));

            // Initialize the platform's GL state
            _platform.SetExternalGL(_gl);

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Generate an L-System from JSON and prepare geometry for upload.
    /// Can be called from any thread. Returns true if geometry was produced.
    /// </summary>
    public bool GenerateLSystem(string definitionJson, int iterations)
    {
        try
        {
            var loader = new builtin.tools.Lindenmayer.LSystemLoader();
            var definition = loader.LoadDefinition(definitionJson);
            var system = loader.CreateSystem(definition);
            var generator = new builtin.tools.Lindenmayer.LGenerator(system, "preview");
            var instance = generator.Generate(iterations);

            var interpreter = new builtin.tools.Lindenmayer.AlphaInterpreter(instance);
            var matMesh = new MatMesh();
            interpreter.Run(null, Vector3.Zero, matMesh, null);

            if (matMesh.IsEmpty())
            {
                lock (_lo) { _pendingInstanceDesc = null; }
                return false;
            }

            var id = InstanceDesc.CreateFromMatMesh(matMesh, 500f);
            lock (_lo) { _pendingInstanceDesc = id; }
            return true;
        }
        catch
        {
            lock (_lo) { _pendingInstanceDesc = null; }
            return false;
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

        // Add a directional light for basic illumination
        renderFrame.ListDirectionalLights.Add(
            new DirectionalLight(new Vector4(1f, 1f, 1f, 1f)));

        // Add ambient light so geometry is visible without full ECS lighting
        renderFrame.ListAmbientLights.Add(
            new AmbientLight(new Vector4(0.3f, 0.3f, 0.35f, 1f)));

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
