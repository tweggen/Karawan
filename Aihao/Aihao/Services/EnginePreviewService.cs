using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Avalonia.OpenGL;
using engine;
using engine.joyce;
using engine.joyce.components;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Owns the headless engine lifecycle for preview rendering.
/// Bootstraps the engine, registers services, creates lights, then delegates
/// GL initialization and rendering to PreviewHelper.
/// </summary>
public sealed class EnginePreviewService
{
    private static readonly object _lo = new();
    private static EnginePreviewService? _instance;

    private engine.Engine? _engine;
    private Platform? _platform;

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
    /// Initialize the headless engine and GL context. Must be called from the GL thread.
    /// </summary>
    public void Initialize(GlInterface glInterface)
    {
        if (PreviewHelper.Instance.IsInitialized) return;
        if (string.IsNullOrEmpty(ResourcePath)) return;

        // Set up resource path so the engine can find shaders
        engine.GlobalSettings.Set("Engine.ResourcePath", ResourcePath!);

        // Set up graphics API settings (required by ShaderSource for GLSL version header)
        if (string.IsNullOrEmpty(engine.GlobalSettings.Get("platform.threeD.API")))
        {
            engine.GlobalSettings.Set("platform.threeD.API", "OpenGL");
            engine.GlobalSettings.Set("platform.threeD.API.version",
                System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX) ? "410" : "430");
        }

        // Register a minimal asset implementation before engine creation
        _ = new HeadlessAssetImplementation(ResourcePath!);

        // Register TextureCatalogue before engine creation
        I.Register<TextureCatalogue>(() => new TextureCatalogue());

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

        // Initialize PreviewHelper for GL-only setup
        PreviewHelper.Instance.Initialize(glInterface.GetProcAddress, _platform);
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
