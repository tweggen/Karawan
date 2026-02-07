using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels.LSystem;

/// <summary>
/// Tree item for the L-System list (definitions and configs).
/// </summary>
public partial class LSystemTreeItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _type = "definition"; // "definition" or "config"
    [ObservableProperty] private bool _isExpanded;
}

/// <summary>
/// ViewModel for a configuration entry (for parametric L-systems like houses).
/// </summary>
public partial class LSystemConfigViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private float _storyHeight = 3.0f;
    [ObservableProperty] private int _minSegmentStories = 4;
    [ObservableProperty] private float _shrinkAmount = 2.0f;
    [ObservableProperty] private float _segmentProbability = 0.8f;

    public ObservableCollection<LSystemParamViewModel> Materials { get; } = new();

    private Action? _onModified;

    public void SetModifiedCallback(Action callback)
    {
        _onModified = callback;
        foreach (var mat in Materials)
            mat.SetModifiedCallback(callback);
    }

    partial void OnNameChanged(string value) => _onModified?.Invoke();
    partial void OnStoryHeightChanged(float value) => _onModified?.Invoke();
    partial void OnMinSegmentStoriesChanged(int value) => _onModified?.Invoke();
    partial void OnShrinkAmountChanged(float value) => _onModified?.Invoke();
    partial void OnSegmentProbabilityChanged(float value) => _onModified?.Invoke();

    public void LoadFromJson(string name, JsonObject obj)
    {
        Name = name;

        if (obj.TryGetPropertyValue("storyHeight", out var shNode))
            StoryHeight = GetFloat(shNode);
        if (obj.TryGetPropertyValue("minSegmentStories", out var msNode))
            MinSegmentStories = GetInt(msNode);
        if (obj.TryGetPropertyValue("shrinkAmount", out var saNode))
            ShrinkAmount = GetFloat(saNode);
        if (obj.TryGetPropertyValue("segmentProbability", out var spNode))
            SegmentProbability = GetFloat(spNode);

        Materials.Clear();
        if (obj.TryGetPropertyValue("materials", out var matsNode) && matsNode is JsonObject matsObj)
        {
            foreach (var kvp in matsObj)
            {
                var mat = new LSystemParamViewModel { Key = kvp.Key, Value = kvp.Value?.GetValue<string>() ?? "" };
                mat.SetModifiedCallback(_onModified ?? (() => {}));
                Materials.Add(mat);
            }
        }
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject
        {
            ["storyHeight"] = StoryHeight,
            ["minSegmentStories"] = MinSegmentStories,
            ["shrinkAmount"] = ShrinkAmount,
            ["segmentProbability"] = SegmentProbability
        };

        if (Materials.Count > 0)
        {
            var matsObj = new JsonObject();
            foreach (var mat in Materials)
            {
                matsObj[mat.Key] = mat.Value;
            }
            obj["materials"] = matsObj;
        }

        return obj;
    }

    private static float GetFloat(JsonNode? node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<float>(out var f)) return f;
            if (jv.TryGetValue<double>(out var d)) return (float)d;
            if (jv.TryGetValue<int>(out var i)) return i;
        }
        return 0f;
    }

    private static int GetInt(JsonNode? node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<float>(out var f)) return (int)f;
            if (jv.TryGetValue<double>(out var d)) return (int)d;
        }
        return 0;
    }

    [RelayCommand]
    private void AddMaterial()
    {
        var mat = new LSystemParamViewModel { Key = "material", Value = "materials/default" };
        mat.SetModifiedCallback(_onModified ?? (() => {}));
        Materials.Add(mat);
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemoveMaterial(LSystemParamViewModel? mat)
    {
        if (mat != null && Materials.Remove(mat))
        {
            _onModified?.Invoke();
        }
    }
}

/// <summary>
/// Top-level editor ViewModel for the lsystems section.
/// Manages all L-system definitions and configurations.
/// </summary>
public partial class LSystemEditorViewModel : ObservableObject
{
    [ObservableProperty] private bool _isDirty;

    /// <summary>
    /// Preview ViewModel for the 3D preview pane.
    /// </summary>
    public LSystemPreviewViewModel Preview { get; } = new();

    /// <summary>
    /// Tree of L-system definitions and configs.
    /// </summary>
    public ObservableCollection<LSystemTreeItem> TreeItems { get; } = new();

    /// <summary>
    /// All L-system definitions.
    /// </summary>
    public ObservableCollection<LSystemDefinitionViewModel> Definitions { get; } = new();

    /// <summary>
    /// All L-system configurations (for parametric systems).
    /// Maps definition name to its embedded config (for parametric L-systems).
    /// </summary>
    public ObservableCollection<LSystemConfigViewModel> Configs { get; } = new();

    [ObservableProperty] private object? _selectedTreeItem;
    [ObservableProperty] private LSystemDefinitionViewModel? _selectedDefinition;
    [ObservableProperty] private LSystemConfigViewModel? _selectedConfig;

    /// <summary>
    /// The currently selected editable item (definition or config).
    /// </summary>
    [ObservableProperty] private object? _currentEditor;

    // Internal storage
    private JsonObject? _fullLSystemsObj;

    // Track whether we loaded from array format (true) or object format (false)
    private bool _usesArrayFormat;

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (value is LSystemTreeItem item)
        {
            if (item.Type == "definition")
            {
                var def = Definitions.FirstOrDefault(d => d.Name == item.Name);
                SelectedDefinition = def;
                SelectedConfig = null;
                CurrentEditor = def;
                Preview.SetDefinition(def);
            }
            else if (item.Type == "config")
            {
                var cfg = Configs.FirstOrDefault(c => c.Name == item.Name);
                SelectedConfig = cfg;
                SelectedDefinition = null;
                CurrentEditor = cfg;
                Preview.SetDefinition(null);
            }
        }
    }

    public void LoadFromJson(JsonObject lsystemsObj)
    {
        _fullLSystemsObj = lsystemsObj;
        TreeItems.Clear();
        Definitions.Clear();
        Configs.Clear();
        SelectedTreeItem = null;
        SelectedDefinition = null;
        SelectedConfig = null;
        CurrentEditor = null;

        // Try array format first (actual file format: { "lsystems": [...] })
        if (lsystemsObj.TryGetPropertyValue("lsystems", out var lsystemsNode) && lsystemsNode is JsonArray lsystemsArr)
        {
            _usesArrayFormat = true;
            foreach (var item in lsystemsArr)
            {
                if (item is JsonObject itemObj)
                {
                    var name = itemObj["name"]?.GetValue<string>() ?? "";
                    var type = itemObj["type"]?.GetValue<string>() ?? "";

                    // All items are definitions (they have seed, rules, macros)
                    var def = new LSystemDefinitionViewModel();
                    def.LoadFromJson(name, itemObj);
                    def.SetModifiedCallback(() => IsDirty = true);
                    Definitions.Add(def);

                    TreeItems.Add(new LSystemTreeItem { Name = name, Type = "definition" });

                    // Parametric items also have embedded config
                    if (type == "parametric" && itemObj.TryGetPropertyValue("config", out var cfgNode) && cfgNode is JsonObject cfgObj)
                    {
                        var cfg = new LSystemConfigViewModel();
                        cfg.LoadFromJson(name, cfgObj);
                        cfg.SetModifiedCallback(() => IsDirty = true);
                        Configs.Add(cfg);
                        // Note: config is associated with the definition by name, not a separate tree item
                    }
                }
            }
        }
        // Fall back to object format (for compatibility: { "definitions": {...}, "configs": {...} })
        else
        {
            _usesArrayFormat = false;

            // Load definitions
            if (lsystemsObj.TryGetPropertyValue("definitions", out var defsNode) && defsNode is JsonObject defsObj)
            {
                foreach (var kvp in defsObj)
                {
                    if (kvp.Value is JsonObject defObj)
                    {
                        var def = new LSystemDefinitionViewModel();
                        def.LoadFromJson(kvp.Key, defObj);
                        def.SetModifiedCallback(() => IsDirty = true);
                        Definitions.Add(def);

                        TreeItems.Add(new LSystemTreeItem { Name = kvp.Key, Type = "definition" });
                    }
                }
            }

            // Load configurations
            if (lsystemsObj.TryGetPropertyValue("configs", out var cfgsNode) && cfgsNode is JsonObject cfgsObj)
            {
                foreach (var kvp in cfgsObj)
                {
                    if (kvp.Value is JsonObject cfgObj)
                    {
                        var cfg = new LSystemConfigViewModel();
                        cfg.LoadFromJson(kvp.Key, cfgObj);
                        cfg.SetModifiedCallback(() => IsDirty = true);
                        Configs.Add(cfg);

                        TreeItems.Add(new LSystemTreeItem { Name = kvp.Key, Type = "config" });
                    }
                }
            }
        }

        // Select first item
        if (TreeItems.Count > 0)
        {
            SelectedTreeItem = TreeItems[0];
        }

        IsDirty = false;
    }

    public JsonObject ToJson()
    {
        var result = new JsonObject();

        if (_usesArrayFormat)
        {
            // Write in array format: { "lsystems": [...] }
            var lsystemsArr = new JsonArray();
            foreach (var def in Definitions)
            {
                var defJson = def.ToJson();

                // Check if this definition has an associated config (parametric L-system)
                var cfg = Configs.FirstOrDefault(c => c.Name == def.Name);
                if (cfg != null)
                {
                    defJson["type"] = "parametric";
                    defJson["config"] = cfg.ToJson();
                }

                lsystemsArr.Add(defJson);
            }
            result["lsystems"] = lsystemsArr;
        }
        else
        {
            // Write in object format: { "definitions": {...}, "configs": {...} }
            var defsObj = new JsonObject();
            foreach (var def in Definitions)
            {
                defsObj[def.Name] = def.ToJson();
            }
            result["definitions"] = defsObj;

            if (Configs.Count > 0)
            {
                var cfgsObj = new JsonObject();
                foreach (var cfg in Configs)
                {
                    cfgsObj[cfg.Name] = cfg.ToJson();
                }
                result["configs"] = cfgsObj;
            }
        }

        return result;
    }

    [RelayCommand]
    private void AddDefinition()
    {
        var name = $"newLSystem{Definitions.Count + 1}";
        var def = new LSystemDefinitionViewModel { Name = name };
        def.SetModifiedCallback(() => IsDirty = true);
        Definitions.Add(def);
        TreeItems.Add(new LSystemTreeItem { Name = name, Type = "definition" });
        SelectedTreeItem = TreeItems.Last();
        IsDirty = true;
    }

    [RelayCommand]
    private void AddConfig()
    {
        var name = $"newConfig{Configs.Count + 1}";
        var cfg = new LSystemConfigViewModel { Name = name };
        cfg.SetModifiedCallback(() => IsDirty = true);
        Configs.Add(cfg);
        TreeItems.Add(new LSystemTreeItem { Name = name, Type = "config" });
        SelectedTreeItem = TreeItems.Last();
        IsDirty = true;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedTreeItem is LSystemTreeItem item)
        {
            if (item.Type == "definition")
            {
                var def = Definitions.FirstOrDefault(d => d.Name == item.Name);
                if (def != null)
                {
                    Definitions.Remove(def);
                    TreeItems.Remove(item);
                    SelectedTreeItem = TreeItems.FirstOrDefault();
                    IsDirty = true;
                }
            }
            else if (item.Type == "config")
            {
                var cfg = Configs.FirstOrDefault(c => c.Name == item.Name);
                if (cfg != null)
                {
                    Configs.Remove(cfg);
                    TreeItems.Remove(item);
                    SelectedTreeItem = TreeItems.FirstOrDefault();
                    IsDirty = true;
                }
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        // The actual save is handled by the document/project service.
        IsDirty = false;
    }
}
