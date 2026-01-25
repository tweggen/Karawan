using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service for loading and saving per-user application settings.
/// Settings are stored in a platform-specific location.
/// </summary>
public class UserSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    private readonly string _settingsPath;
    private UserSettings _settings;
    private bool _isDirty;
    
    /// <summary>
    /// The current user settings.
    /// </summary>
    public UserSettings Settings => _settings;
    
    /// <summary>
    /// Whether settings have been modified since last save.
    /// </summary>
    public bool IsDirty => _isDirty;
    
    /// <summary>
    /// Event raised when settings are modified.
    /// </summary>
    public event EventHandler? SettingsChanged;
    
    public UserSettingsService()
    {
        _settingsPath = GetSettingsPath();
        _settings = new UserSettings();
    }
    
    /// <summary>
    /// Get the platform-specific settings file path.
    /// </summary>
    private static string GetSettingsPath()
    {
        string appDataPath;
        
        if (OperatingSystem.IsWindows())
        {
            // Windows: %APPDATA%\Aihao\settings.json
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: ~/Library/Application Support/Aihao/settings.json
            appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            // Linux/other: ~/.config/Aihao/settings.json
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                configHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");
            }
            appDataPath = configHome;
        }
        
        var aihaoDir = Path.Combine(appDataPath, "Aihao");
        return Path.Combine(aihaoDir, "settings.json");
    }
    
    /// <summary>
    /// Get the directory containing the settings file.
    /// </summary>
    public string GetSettingsDirectory()
    {
        return Path.GetDirectoryName(_settingsPath) ?? string.Empty;
    }
    
    /// <summary>
    /// Load settings from disk. Creates default settings if file doesn't exist.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    _settings = loaded;
                    
                    // Validate recent projects still exist
                    foreach (var project in _settings.RecentProjects)
                    {
                        project.Exists = File.Exists(project.Path);
                    }
                }
            }
            else
            {
                // First run - create default settings
                _settings = new UserSettings();
            }
            
            _isDirty = false;
        }
        catch (Exception ex)
        {
            // Log error but continue with defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            _settings = new UserSettings();
        }
    }
    
    /// <summary>
    /// Save settings to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            _settings.LastSaved = DateTime.UtcNow;
            
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            
            _isDirty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Save settings if auto-save is enabled and settings are dirty.
    /// </summary>
    public async Task AutoSaveIfNeededAsync()
    {
        if (_settings.AutoSaveSettings && _isDirty)
        {
            await SaveAsync();
        }
    }
    
    /// <summary>
    /// Mark settings as modified.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    #region Convenience Methods
    
    /// <summary>
    /// Add a project to recent projects and optionally auto-save.
    /// </summary>
    public async Task AddRecentProjectAsync(string path, string? name = null)
    {
        _settings.AddRecentProject(path, name);
        MarkDirty();
        await AutoSaveIfNeededAsync();
    }
    
    /// <summary>
    /// Update last directory and optionally auto-save.
    /// </summary>
    public async Task SetLastDirectoryAsync(string? directory)
    {
        if (_settings.LastDirectory != directory)
        {
            _settings.LastDirectory = directory;
            MarkDirty();
            await AutoSaveIfNeededAsync();
        }
    }
    
    /// <summary>
    /// Update window state.
    /// </summary>
    public void SetWindowState(int? x, int? y, int? width, int? height, bool maximized)
    {
        _settings.WindowX = x;
        _settings.WindowY = y;
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _settings.WindowMaximized = maximized;
        MarkDirty();
    }
    
    /// <summary>
    /// Get module settings with typed access.
    /// </summary>
    public JsonObject GetModuleSettings(string moduleId)
    {
        return _settings.GetModuleSettings(moduleId);
    }
    
    /// <summary>
    /// Set a module setting and mark dirty.
    /// </summary>
    public async Task SetModuleSettingAsync(string moduleId, string key, JsonNode? value)
    {
        _settings.SetModuleSetting(moduleId, key, value);
        MarkDirty();
        await AutoSaveIfNeededAsync();
    }
    
    /// <summary>
    /// Get a typed module setting.
    /// </summary>
    public T? GetModuleSetting<T>(string moduleId, string key, T? defaultValue = default)
    {
        return _settings.GetModuleSetting<T>(moduleId, key, defaultValue);
    }
    
    #endregion
}
