using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Aihao.Graphics;
using Avalonia.OpenGL;
using engine;
using engine.joyce;
using engine.joyce.components;
using Silk.NET.OpenGL;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Owns the headless engine lifecycle for preview rendering.
/// Bootstraps the engine, registers services, creates ECS entities (camera, geometry, lights),
/// then delegates GL initialization and rendering to PreviewHelper.
/// </summary>
public sealed class EnginePreviewService
{
    private static readonly object _lo = new();
    private static EnginePreviewService? _instance;

    private engine.Engine? _engine;
    private Platform? _platform;

    private DefaultEcs.Entity _cameraEntity;
    private DefaultEcs.Entity _geometryEntity;
    private InstanceDesc? _pendingInstanceDesc;
    private bool _pendingClear;

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
    /// Thread-safe setter for pending mesh geometry.
    /// The geometry will be applied to the ECS entity on the next RenderPreview call (GL thread).
    /// </summary>
    public void SetInstanceDesc(InstanceDesc id)
    {
        lock (_lo) { _pendingInstanceDesc = id; _pendingClear = false; }
    }

    /// <summary>
    /// Clear any pending or current geometry.
    /// </summary>
    public void ClearInstanceDesc()
    {
        lock (_lo) { _pendingInstanceDesc = null; _pendingClear = true; }
    }

    /// <summary>
    /// Initialize the headless engine and GL context. Must be called from the GL thread.
    /// </summary>
    public void Initialize(GlInterface glInterface)
    {
        if (PreviewHelper.Instance.IsInitialized) return;
        if (string.IsNullOrEmpty(ResourcePath)) return;

        // Set up resource path so the engine can find shaders
        engine.GlobalSettings.Set("Engine.ResourcePath", ResourcePath!);

        // Detect actual GL version from Avalonia's context before setting shader version
        if (string.IsNullOrEmpty(engine.GlobalSettings.Get("platform.threeD.API")))
        {
            _detectAndSetGlVersion(glInterface);
        }

        // Register a minimal asset implementation before engine creation
        _ = new HeadlessAssetImplementation(ResourcePath!);

        // Register TextureCatalogue before engine creation
        I.Register<TextureCatalogue>(() => new TextureCatalogue());

        // Register a minimal casette.Loader with empty config so engine subsystems
        // (SceneSequencer, GlobalSettings, etc.) that call WhenLoaded() don't fail.
        I.Register<engine.casette.Loader>(() =>
            new engine.casette.Loader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"))));

        // Create the headless engine + platform
        _engine = Platform.EasyCreateHeadless(Array.Empty<string>(), out _platform);

        // Mark headless so ECS world access works without a logical thread.
        _engine.SetHeadless();

        // Register a synthetic "rgba" atlas entry so FindColorTexture works
        var tc = I.Get<TextureCatalogue>();
        tc.AddAtlasEntry("rgba", "rgba", Vector2.Zero, Vector2.One, 64, 64, false);

        // Create ECS light entities so LightCollector.CollectLights() finds them
        var eAmbient = _engine.CreateEntity("Preview.AmbientLight");
        eAmbient.Set(new AmbientLight(new Vector4(0.3f, 0.3f, 0.35f, 1f)));

        var eLight = _engine.CreateEntity("Preview.DirectionalLight");
        eLight.Set(new DirectionalLight(new Vector4(0.9f, 0.9f, 0.9f, 1f)));
        var lightRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -45f * MathF.PI / 180f);
        var lightMatrix = Matrix4x4.CreateFromQuaternion(lightRotation);
        eLight.Set(new Transform3ToWorld(
            0xffffffff, Transform3ToWorld.Visible, lightMatrix));

        // Camera entity (standard ECS camera — same approach as GenericLauncher)
        _cameraEntity = _engine.CreateEntity("Preview.Camera");
        _cameraEntity.Set(new Camera3
        {
            Angle = 60f,
            NearFrustum = 0.1f,
            FarFrustum = 1000f,
            CameraMask = 0xffffffff
        });
        _cameraEntity.Set(new Transform3ToWorld(
            0xffffffff, Transform3ToWorld.Visible, Matrix4x4.Identity));

        // Geometry entity (Instance3 added when geometry arrives)
        _geometryEntity = _engine.CreateEntity("Preview.Geometry");
        _geometryEntity.Set(new Transform3ToWorld(
            0xffffffff, Transform3ToWorld.Visible, Matrix4x4.Identity));

        // Initialize PreviewHelper for GL-only setup
        PreviewHelper.Instance.Initialize(glInterface.GetProcAddress, _platform);
    }

    /// <summary>
    /// Render the current preview geometry. Must be called from the GL thread.
    /// Applies pending geometry to ECS entities, updates the camera, then delegates to PreviewHelper.
    /// </summary>
    public void RenderPreview(int width, int height, uint targetFbo,
        float cameraDistance, float cameraYaw, float cameraPitch)
    {
        // Apply pending geometry on GL thread (thread-safe handoff)
        InstanceDesc? newId;
        bool doClear;
        lock (_lo)
        {
            newId = _pendingInstanceDesc;
            doClear = _pendingClear;
            _pendingInstanceDesc = null;
            _pendingClear = false;
        }

        if (doClear)
        {
            if (_geometryEntity.Has<Instance3>())
                _geometryEntity.Remove<Instance3>();
            System.Console.Error.WriteLine("[EnginePreview] Cleared geometry.");
        }
        else if (newId != null)
        {
            _geometryEntity.Set(new Instance3(newId));
            System.Console.Error.WriteLine(
                $"[EnginePreview] Set Instance3: {newId.Meshes?.Count ?? 0} meshes, {newId.Materials?.Count ?? 0} materials");
        }

        // Update camera transform from orbit parameters
        _cameraEntity.Set(new Transform3ToWorld(
            0xffffffff, Transform3ToWorld.Visible,
            BuildCameraTransform(cameraDistance, cameraYaw, cameraPitch)));

        PreviewHelper.Instance.RenderPreview(width, height, targetFbo);
    }

    private static void _detectAndSetGlVersion(GlInterface glInterface)
    {
        using var ctx = new AvaloniaNativeContext(glInterface);
        var gl = GL.GetApi(ctx);
        string versionStr = gl.GetStringS(Silk.NET.OpenGL.StringName.Version) ?? "";
        System.Console.Error.WriteLine($"[EnginePreview] GL version string: \"{versionStr}\"");

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

        System.Console.Error.WriteLine($"[EnginePreview] Detected API={api}, version={version}");
        engine.GlobalSettings.Set("platform.threeD.API", api);
        engine.GlobalSettings.Set("platform.threeD.API.version", version);
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
    /// Minimal asset implementation for headless mode.
    /// Opens files from the resource path on the filesystem.
    /// </summary>
    private sealed class HeadlessAssetImplementation : IAssetImplementation
    {
        private readonly string _resourcePath;
        private readonly SortedDictionary<string, string> _associations = new();

        private static readonly string[] _probeDirs = ["shaders"];

        public HeadlessAssetImplementation(string resourcePath)
        {
            _resourcePath = resourcePath;
            engine.Assets.SetAssetImplementation(this);
        }

        public Stream Open(in string filename)
        {
            var path = _resolve(filename);
            if (path != null)
                return File.OpenRead(path);

            throw new FileNotFoundException($"Asset not found: {filename} (resourcePath={_resourcePath})", filename);
        }

        public bool Exists(in string filename) => _resolve(filename) != null;

        public void AddAssociation(string tag, string uri) => _associations[tag] = uri;

        public IReadOnlyDictionary<string, string> GetAssets() => _associations;

        private string? _resolve(in string filename)
        {
            var path = Path.Combine(_resourcePath, filename);
            if (File.Exists(path)) return path;

            if (_associations.TryGetValue(filename, out var uri))
            {
                path = Path.Combine(_resourcePath, uri);
                if (File.Exists(path)) return path;
            }

            foreach (var dir in _probeDirs)
            {
                path = Path.Combine(_resourcePath, dir, filename);
                if (File.Exists(path)) return path;
            }

            return null;
        }
    }
}
