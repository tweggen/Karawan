using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// ViewModel for a trigger binding in the narration system.
/// </summary>
public partial class NarrationTriggerViewModel : ObservableObject
{
    [ObservableProperty] private string _eventPath = "";
    [ObservableProperty] private string _scriptName = "";
    [ObservableProperty] private string _mode = "conversation";
}

/// <summary>
/// Represents a script as a top-level tree item containing its nodes.
/// </summary>
public partial class ScriptTreeItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isExpanded;
    public ObservableCollection<NarrationNodeViewModel> Nodes { get; } = new();
}

/// <summary>
/// Top-level editor ViewModel for the narration section.
/// Manages all scripts, triggers, bindings, and startup config.
/// </summary>
public partial class NarrationEditorViewModel : ObservableObject
{
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _startup = "";

    /// <summary>
    /// All script names available in this narration config.
    /// </summary>
    public ObservableCollection<string> ScriptNames { get; } = new();

    [ObservableProperty] private string? _selectedScriptName;

    /// <summary>
    /// Tree of scripts with their child nodes.
    /// </summary>
    public ObservableCollection<ScriptTreeItem> ScriptTree { get; } = new();

    /// <summary>
    /// The currently selected item in the tree (ScriptTreeItem or NarrationNodeViewModel).
    /// </summary>
    [ObservableProperty] private object? _selectedTreeItem;

    /// <summary>
    /// Nodes of the currently selected script (reference to the active ScriptTreeItem.Nodes).
    /// </summary>
    public ObservableCollection<NarrationNodeViewModel> Nodes { get; private set; } = new();

    [ObservableProperty] private NarrationNodeViewModel? _selectedNode;

    /// <summary>
    /// Triggers from the narration config.
    /// </summary>
    public ObservableCollection<NarrationTriggerViewModel> Triggers { get; } = new();

    // Internal storage: the full narration JSON and per-script objects
    private JsonObject? _fullNarrationObj;
    private readonly Dictionary<string, JsonObject> _scriptObjects = new();
    private readonly Dictionary<string, string> _scriptStartNodes = new();

    /// <summary>
    /// Start node ID for the currently selected script.
    /// </summary>
    [ObservableProperty] private string _startNodeId = "";

    partial void OnSelectedTreeItemChanged(object? value)
    {
        switch (value)
        {
            case ScriptTreeItem scriptItem:
                if (SelectedScriptName != scriptItem.Name)
                    SelectedScriptName = scriptItem.Name;
                scriptItem.IsExpanded = true;
                break;
            case NarrationNodeViewModel nodeVm:
            {
                // Find the parent script
                var parent = ScriptTree.FirstOrDefault(s => s.Nodes.Contains(nodeVm));
                if (parent != null && SelectedScriptName != parent.Name)
                    SelectedScriptName = parent.Name;
                SelectedNode = nodeVm;
                break;
            }
        }
    }

    public void LoadFromJson(JsonObject narrationObj)
    {
        _fullNarrationObj = narrationObj;
        ScriptNames.Clear();
        ScriptTree.Clear();
        Nodes = new ObservableCollection<NarrationNodeViewModel>();
        Triggers.Clear();
        _scriptObjects.Clear();
        _scriptStartNodes.Clear();
        SelectedScriptName = null;
        SelectedNode = null;
        SelectedTreeItem = null;

        if (narrationObj.TryGetPropertyValue("startup", out var startupNode))
            Startup = startupNode?.GetValue<string>() ?? "";

        // Load triggers
        if (narrationObj.TryGetPropertyValue("triggers", out var triggersNode) && triggersNode is JsonObject triggersObj)
        {
            foreach (var kvp in triggersObj)
            {
                if (kvp.Value is JsonObject trigObj)
                {
                    var trigger = new NarrationTriggerViewModel { EventPath = kvp.Key };
                    if (trigObj.TryGetPropertyValue("script", out var sn))
                        trigger.ScriptName = sn?.GetValue<string>() ?? "";
                    if (trigObj.TryGetPropertyValue("mode", out var mn))
                        trigger.Mode = mn?.GetValue<string>() ?? "conversation";
                    Triggers.Add(trigger);
                }
            }
        }

        // Load script names and build tree
        if (narrationObj.TryGetPropertyValue("scripts", out var scriptsNode) && scriptsNode is JsonObject scriptsObj)
        {
            foreach (var kvp in scriptsObj)
            {
                ScriptNames.Add(kvp.Key);
                if (kvp.Value is JsonObject scriptObj)
                {
                    _scriptObjects[kvp.Key] = scriptObj;
                    if (scriptObj.TryGetPropertyValue("start", out var startNode))
                        _scriptStartNodes[kvp.Key] = startNode?.GetValue<string>() ?? "";
                }

                var treeItem = new ScriptTreeItem { Name = kvp.Key };
                PopulateTreeItemNodes(treeItem, kvp.Key);
                ScriptTree.Add(treeItem);
            }

            // Select first script (or startup script)
            if (ScriptNames.Count > 0)
            {
                var targetName = ScriptNames.Contains(Startup) ? Startup : ScriptNames[0];
                SelectedScriptName = targetName;
                var targetItem = ScriptTree.FirstOrDefault(s => s.Name == targetName);
                if (targetItem != null)
                {
                    targetItem.IsExpanded = true;
                    SelectedTreeItem = targetItem;
                }
            }
        }

        IsDirty = false;
    }

    private void PopulateTreeItemNodes(ScriptTreeItem treeItem, string scriptName)
    {
        treeItem.Nodes.Clear();
        if (!_scriptObjects.TryGetValue(scriptName, out var scriptObj))
            return;

        if (scriptObj.TryGetPropertyValue("nodes", out var nodesNode) && nodesNode is JsonObject nodesObj)
        {
            foreach (var kvp in nodesObj)
            {
                if (kvp.Value is JsonObject nodeObj)
                {
                    var nodeVm = new NarrationNodeViewModel();
                    nodeVm.LoadFromJson(kvp.Key, nodeObj);
                    nodeVm.InitializeMarkup();
                    nodeVm.SetModifiedCallback(() => IsDirty = true);
                    treeItem.Nodes.Add(nodeVm);
                }
            }
        }
    }

    partial void OnSelectedScriptNameChanged(string? value)
    {
        LoadScript(value);
    }

    private void LoadScript(string? scriptName)
    {
        SelectedNode = null;
        StartNodeId = "";

        if (scriptName == null || !_scriptObjects.TryGetValue(scriptName, out _))
        {
            Nodes = new ObservableCollection<NarrationNodeViewModel>();
            return;
        }

        if (_scriptStartNodes.TryGetValue(scriptName, out var startId))
            StartNodeId = startId;

        // Point Nodes to the tree item's nodes collection
        var treeItem = ScriptTree.FirstOrDefault(s => s.Name == scriptName);
        if (treeItem != null)
        {
            Nodes = treeItem.Nodes;
        }

        // Select start node or first
        SelectedNode = Nodes.FirstOrDefault(n => n.NodeId == StartNodeId) ?? Nodes.FirstOrDefault();
    }

    public JsonObject ToJson()
    {
        // Start from the original object to preserve bindings and other fields
        var result = _fullNarrationObj?.DeepClone() as JsonObject ?? new JsonObject();

        result["startup"] = Startup;

        // Write triggers
        var triggersObj = new JsonObject();
        foreach (var t in Triggers)
        {
            var tObj = new JsonObject { ["script"] = t.ScriptName, ["mode"] = t.Mode };
            triggersObj[t.EventPath] = tObj;
        }
        result["triggers"] = triggersObj;

        // Write back the currently loaded script's nodes
        if (SelectedScriptName != null)
        {
            SaveCurrentScriptToStorage();
        }

        // Save all scripts from tree
        foreach (var item in ScriptTree)
        {
            SaveScriptTreeItemToStorage(item);
        }

        // Rebuild scripts object
        var scriptsObj = new JsonObject();
        foreach (var name in ScriptNames)
        {
            if (_scriptObjects.TryGetValue(name, out var sObj))
            {
                scriptsObj[name] = sObj.DeepClone();
            }
        }
        result["scripts"] = scriptsObj;

        return result;
    }

    private void SaveScriptTreeItemToStorage(ScriptTreeItem item)
    {
        var scriptObj = new JsonObject();
        if (_scriptStartNodes.TryGetValue(item.Name, out var startId) && !string.IsNullOrEmpty(startId))
            scriptObj["start"] = startId;

        var nodesObj = new JsonObject();
        foreach (var node in item.Nodes)
        {
            nodesObj[node.NodeId] = node.ToJson();
        }
        scriptObj["nodes"] = nodesObj;

        _scriptObjects[item.Name] = scriptObj;
    }

    private void SaveCurrentScriptToStorage()
    {
        if (SelectedScriptName == null) return;

        var treeItem = ScriptTree.FirstOrDefault(s => s.Name == SelectedScriptName);
        if (treeItem != null)
        {
            SaveScriptTreeItemToStorage(treeItem);
            _scriptStartNodes[SelectedScriptName] = StartNodeId;
        }
    }

    [RelayCommand]
    private void AddNode()
    {
        if (SelectedScriptName == null) return;

        var treeItem = ScriptTree.FirstOrDefault(s => s.Name == SelectedScriptName);
        if (treeItem == null) return;

        var id = $"newNode{treeItem.Nodes.Count + 1}";
        var node = new NarrationNodeViewModel { NodeId = id };
        node.SetModifiedCallback(() => IsDirty = true);
        treeItem.Nodes.Add(node);
        treeItem.IsExpanded = true;
        SelectedNode = node;
        SelectedTreeItem = node;
        IsDirty = true;
    }

    [RelayCommand]
    private void RemoveNode(NarrationNodeViewModel? node)
    {
        if (node == null) return;

        var parent = ScriptTree.FirstOrDefault(s => s.Nodes.Contains(node));
        if (parent != null && parent.Nodes.Remove(node))
        {
            if (SelectedNode == node)
            {
                SelectedNode = parent.Nodes.FirstOrDefault();
                SelectedTreeItem = SelectedNode ?? (object)parent;
            }
            IsDirty = true;
        }
    }

    [RelayCommand]
    private void AddScript()
    {
        var name = $"newScript{ScriptNames.Count + 1}";
        ScriptNames.Add(name);
        _scriptObjects[name] = new JsonObject { ["start"] = "start", ["nodes"] = new JsonObject() };
        _scriptStartNodes[name] = "start";

        var treeItem = new ScriptTreeItem { Name = name, IsExpanded = true };
        ScriptTree.Add(treeItem);

        SelectedScriptName = name;
        SelectedTreeItem = treeItem;
        IsDirty = true;
    }

    [RelayCommand]
    private void Save()
    {
        // The actual save is handled by the document/project service.
        // This just marks clean after external save.
        IsDirty = false;
    }
}
