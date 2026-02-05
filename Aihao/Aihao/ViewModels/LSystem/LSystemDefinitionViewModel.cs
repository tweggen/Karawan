using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels.LSystem;

/// <summary>
/// ViewModel for a complete L-system definition.
/// </summary>
public partial class LSystemDefinitionViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";

    /// <summary>
    /// The seed parts (initial state).
    /// </summary>
    public ObservableCollection<LSystemPartViewModel> Seed { get; } = new();

    /// <summary>
    /// The transformation rules.
    /// </summary>
    public ObservableCollection<LSystemRuleViewModel> Rules { get; } = new();

    /// <summary>
    /// The macro rules (final expansion).
    /// </summary>
    public ObservableCollection<LSystemRuleViewModel> Macros { get; } = new();

    [ObservableProperty] private LSystemPartViewModel? _selectedSeedPart;
    [ObservableProperty] private LSystemRuleViewModel? _selectedRule;
    [ObservableProperty] private LSystemRuleViewModel? _selectedMacro;

    /// <summary>
    /// Currently selected item for the inspector panel.
    /// Can be a seed part, rule, or macro.
    /// </summary>
    [ObservableProperty] private object? _selectedItem;

    private Action? _onModified;

    public void SetModifiedCallback(Action callback)
    {
        _onModified = callback;
        foreach (var part in Seed)
            part.SetModifiedCallback(callback);
        foreach (var rule in Rules)
            rule.SetModifiedCallback(callback);
        foreach (var macro in Macros)
            macro.SetModifiedCallback(callback);
    }

    partial void OnNameChanged(string value) => _onModified?.Invoke();

    partial void OnSelectedSeedPartChanged(LSystemPartViewModel? value)
    {
        if (value != null) SelectedItem = value;
    }

    partial void OnSelectedRuleChanged(LSystemRuleViewModel? value)
    {
        if (value != null) SelectedItem = value;
    }

    partial void OnSelectedMacroChanged(LSystemRuleViewModel? value)
    {
        if (value != null) SelectedItem = value;
    }

    public void LoadFromJson(string name, JsonObject obj)
    {
        Name = name;

        Seed.Clear();
        if (obj.TryGetPropertyValue("seed", out var seedNode) && seedNode is JsonObject seedObj)
        {
            if (seedObj.TryGetPropertyValue("parts", out var partsNode) && partsNode is JsonArray partsArr)
            {
                foreach (var item in partsArr)
                {
                    if (item is JsonObject partObj)
                    {
                        var part = new LSystemPartViewModel();
                        part.LoadFromJson(partObj);
                        part.SetModifiedCallback(_onModified ?? (() => {}));
                        Seed.Add(part);
                    }
                }
            }
        }

        Rules.Clear();
        if (obj.TryGetPropertyValue("rules", out var rulesNode) && rulesNode is JsonArray rulesArr)
        {
            foreach (var item in rulesArr)
            {
                if (item is JsonObject ruleObj)
                {
                    var rule = new LSystemRuleViewModel();
                    rule.LoadFromJson(ruleObj);
                    rule.SetModifiedCallback(_onModified ?? (() => {}));
                    Rules.Add(rule);
                }
            }
        }

        Macros.Clear();
        if (obj.TryGetPropertyValue("macros", out var macrosNode) && macrosNode is JsonArray macrosArr)
        {
            foreach (var item in macrosArr)
            {
                if (item is JsonObject macroObj)
                {
                    var macro = new LSystemRuleViewModel();
                    macro.LoadFromJson(macroObj);
                    macro.SetModifiedCallback(_onModified ?? (() => {}));
                    Macros.Add(macro);
                }
            }
        }
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject { ["name"] = Name };

        // Seed
        var seedPartsArr = new JsonArray();
        foreach (var part in Seed)
        {
            seedPartsArr.Add(part.ToJson());
        }
        obj["seed"] = new JsonObject { ["parts"] = seedPartsArr };

        // Rules
        var rulesArr = new JsonArray();
        foreach (var rule in Rules)
        {
            rulesArr.Add(rule.ToJson());
        }
        obj["rules"] = rulesArr;

        // Macros
        if (Macros.Count > 0)
        {
            var macrosArr = new JsonArray();
            foreach (var macro in Macros)
            {
                macrosArr.Add(macro.ToJson());
            }
            obj["macros"] = macrosArr;
        }

        return obj;
    }

    #region Seed Commands

    [RelayCommand]
    private void AddSeedPart()
    {
        var part = new LSystemPartViewModel { Name = "stem(r,l)" };
        part.SetModifiedCallback(_onModified ?? (() => {}));
        Seed.Add(part);
        SelectedSeedPart = part;
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemoveSeedPart(LSystemPartViewModel? part)
    {
        if (part != null && Seed.Remove(part))
        {
            if (SelectedSeedPart == part)
                SelectedSeedPart = Seed.FirstOrDefault();
            _onModified?.Invoke();
        }
    }

    #endregion

    #region Rule Commands

    [RelayCommand]
    private void AddRule()
    {
        var rule = new LSystemRuleViewModel { Match = "stem(r,l)" };
        rule.SetModifiedCallback(_onModified ?? (() => {}));
        Rules.Add(rule);
        SelectedRule = rule;
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemoveRule(LSystemRuleViewModel? rule)
    {
        if (rule != null && Rules.Remove(rule))
        {
            if (SelectedRule == rule)
                SelectedRule = Rules.FirstOrDefault();
            _onModified?.Invoke();
        }
    }

    #endregion

    #region Macro Commands

    [RelayCommand]
    private void AddMacro()
    {
        var macro = new LSystemRuleViewModel { Match = "stem(r,l)" };
        macro.SetModifiedCallback(_onModified ?? (() => {}));
        Macros.Add(macro);
        SelectedMacro = macro;
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemoveMacro(LSystemRuleViewModel? macro)
    {
        if (macro != null && Macros.Remove(macro))
        {
            if (SelectedMacro == macro)
                SelectedMacro = Macros.FirstOrDefault();
            _onModified?.Invoke();
        }
    }

    #endregion
}
