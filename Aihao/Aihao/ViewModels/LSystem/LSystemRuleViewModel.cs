using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels.LSystem;

/// <summary>
/// ViewModel for an L-system rule (transformation rule).
/// </summary>
public partial class LSystemRuleViewModel : ObservableObject
{
    [ObservableProperty] private string _match = "";
    [ObservableProperty] private float _probability = 1.0f;
    [ObservableProperty] private string _condition = "";

    /// <summary>
    /// The list of parts that this rule produces.
    /// </summary>
    public ObservableCollection<LSystemPartViewModel> Transform { get; } = new();

    [ObservableProperty] private LSystemPartViewModel? _selectedPart;

    private Action? _onModified;

    public void SetModifiedCallback(Action callback)
    {
        _onModified = callback;
        foreach (var part in Transform)
        {
            part.SetModifiedCallback(callback);
        }
    }

    partial void OnMatchChanged(string value) => _onModified?.Invoke();
    partial void OnProbabilityChanged(float value) => _onModified?.Invoke();
    partial void OnConditionChanged(string value) => _onModified?.Invoke();

    public void LoadFromJson(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("match", out var matchNode))
            Match = matchNode?.GetValue<string>() ?? "";

        if (obj.TryGetPropertyValue("probability", out var probNode))
        {
            if (probNode is JsonValue jv && jv.TryGetValue<float>(out var prob))
                Probability = prob;
            else if (probNode is JsonValue jv2 && jv2.TryGetValue<double>(out var probD))
                Probability = (float)probD;
        }

        if (obj.TryGetPropertyValue("condition", out var condNode))
            Condition = condNode?.GetValue<string>() ?? "";

        Transform.Clear();
        if (obj.TryGetPropertyValue("transform", out var transformNode) && transformNode is JsonArray transformArr)
        {
            foreach (var item in transformArr)
            {
                if (item is JsonObject partObj)
                {
                    var part = new LSystemPartViewModel();
                    part.LoadFromJson(partObj);
                    part.SetModifiedCallback(_onModified ?? (() => {}));
                    Transform.Add(part);
                }
            }
        }
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject { ["match"] = Match };

        if (Math.Abs(Probability - 1.0f) > 0.001f)
            obj["probability"] = Probability;

        if (!string.IsNullOrEmpty(Condition))
            obj["condition"] = Condition;

        var transformArr = new JsonArray();
        foreach (var part in Transform)
        {
            transformArr.Add(part.ToJson());
        }
        obj["transform"] = transformArr;

        return obj;
    }

    [RelayCommand]
    private void AddPart()
    {
        var part = new LSystemPartViewModel { Name = "newPart()" };
        part.SetModifiedCallback(_onModified ?? (() => {}));
        Transform.Add(part);
        SelectedPart = part;
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemovePart(LSystemPartViewModel? part)
    {
        if (part != null && Transform.Remove(part))
        {
            if (SelectedPart == part)
                SelectedPart = Transform.FirstOrDefault();
            _onModified?.Invoke();
        }
    }

    [RelayCommand]
    private void MovePartUp(LSystemPartViewModel? part)
    {
        if (part == null) return;
        var idx = Transform.IndexOf(part);
        if (idx > 0)
        {
            Transform.Move(idx, idx - 1);
            _onModified?.Invoke();
        }
    }

    [RelayCommand]
    private void MovePartDown(LSystemPartViewModel? part)
    {
        if (part == null) return;
        var idx = Transform.IndexOf(part);
        if (idx >= 0 && idx < Transform.Count - 1)
        {
            Transform.Move(idx, idx + 1);
            _onModified?.Invoke();
        }
    }
}
