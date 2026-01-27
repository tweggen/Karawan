using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace engine;

/// <summary>
/// Configuration for launching a game with the engine.
/// This decouples the platform stubs from any specific game.
/// </summary>
public class LaunchConfig
{
    public class GameConfig
    {
        /// <summary>
        /// Path to the game's main JSON configuration file (e.g., "nogame.json")
        /// </summary>
        [JsonPropertyName("configPath")]
        public string ConfigPath { get; set; } = "game.json";
    }

    public class BrandingConfig
    {
        [JsonPropertyName("vendor")]
        public string Vendor { get; set; } = "Karawan Engine";

        [JsonPropertyName("appName")]
        public string AppName { get; set; } = "karawan";

        [JsonPropertyName("windowTitle")]
        public string WindowTitle { get; set; } = "Karawan";

        [JsonPropertyName("appIcon")]
        public string AppIcon { get; set; } = "appicon.png";
    }

    public class PlatformConfig
    {
        [JsonPropertyName("initialZoomState")]
        public string InitialZoomState { get; set; } = "0";

        [JsonPropertyName("touchControls")]
        public string TouchControls { get; set; } = "false";

        [JsonPropertyName("suspendOnUnfocus")]
        public string SuspendOnUnfocus { get; set; } = "false";

        [JsonPropertyName("createOSD")]
        public string CreateOSD { get; set; } = "false";

        [JsonPropertyName("createUI")]
        public string CreateUI { get; set; } = "true";

        [JsonPropertyName("playTitleMusic")]
        public string PlayTitleMusic { get; set; } = "true";
    }

    [JsonPropertyName("game")]
    public GameConfig Game { get; set; } = new();

    [JsonPropertyName("branding")]
    public BrandingConfig Branding { get; set; } = new();

    [JsonPropertyName("platform")]
    public PlatformConfig Platform { get; set; } = new();

    /// <summary>
    /// Get the read/write path for application data.
    /// </summary>
    public string GetRWPath()
    {
        string userRWPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string vendorRWPath = Path.Combine(userRWPath, Branding.Vendor);
        string appRWPath = Path.Combine(vendorRWPath, Branding.AppName);
        Directory.CreateDirectory(appRWPath);
        return appRWPath;
    }

    /// <summary>
    /// Apply platform-specific settings to GlobalSettings.
    /// </summary>
    public void ApplyToGlobalSettings()
    {
        GlobalSettings.Set("platform.initialZoomState", Platform.InitialZoomState);
        GlobalSettings.Set("splash.touchControls", Platform.TouchControls);
        GlobalSettings.Set("platform.suspendOnUnfocus", Platform.SuspendOnUnfocus);
        GlobalSettings.Set("nogame.CreateOSD", Platform.CreateOSD);
        GlobalSettings.Set("nogame.CreateUI", Platform.CreateUI);
        GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", Platform.PlayTitleMusic);
        GlobalSettings.Set("Engine.RWPath", GetRWPath());
    }

    /// <summary>
    /// Load launch configuration from a JSON file path.
    /// </summary>
    public static LaunchConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LaunchConfig();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<LaunchConfig>(stream, new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        }) ?? new LaunchConfig();
    }

    /// <summary>
    /// Load launch configuration using the engine's asset system.
    /// Call this AFTER the platform's IAssetImplementation has been registered.
    /// </summary>
    public static LaunchConfig LoadFromAssets(string configName = "game.launch.json")
    {
        try
        {
            if (!Assets.Exists(configName))
            {
                return new LaunchConfig();
            }

            using var stream = Assets.Open(configName);
            return JsonSerializer.Deserialize<LaunchConfig>(stream, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            }) ?? new LaunchConfig();
        }
        catch (Exception)
        {
            // Config doesn't exist or can't be parsed - use defaults
            return new LaunchConfig();
        }
    }

    /// <summary>
    /// Load launch configuration, searching in standard locations.
    /// For desktop platforms before asset system is initialized.
    /// </summary>
    public static LaunchConfig LoadFromStandardLocations(string resourcePath)
    {
        // Priority: 
        // 1. ./game.launch.json (working directory)
        // 2. {resourcePath}/game.launch.json
        // 3. Default configuration

        string[] searchPaths =
        {
            "./game.launch.json",
            Path.Combine(resourcePath, "game.launch.json")
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Load(path);
            }
        }

        return new LaunchConfig();
    }
}
