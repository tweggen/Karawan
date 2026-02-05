using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels.LSystem;

/// <summary>
/// ViewModel for a single part (turtle command) in an L-system.
/// Examples: stem(r,l), push(), rotate(d,x,y,z), etc.
/// </summary>
public partial class LSystemPartViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";

    /// <summary>
    /// Parameters as key-value pairs where value can be literal or expression.
    /// </summary>
    public ObservableCollection<LSystemParamViewModel> Parameters { get; } = new();

    private Action? _onModified;

    public void SetModifiedCallback(Action callback)
    {
        _onModified = callback;
        foreach (var param in Parameters)
        {
            param.SetModifiedCallback(callback);
        }
    }

    partial void OnNameChanged(string value) => _onModified?.Invoke();

    public void LoadFromJson(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("name", out var nameNode))
            Name = nameNode?.GetValue<string>() ?? "";

        Parameters.Clear();
        if (obj.TryGetPropertyValue("params", out var paramsNode) && paramsNode is JsonObject paramsObj)
        {
            foreach (var kvp in paramsObj)
            {
                var param = new LSystemParamViewModel { Key = kvp.Key };
                if (kvp.Value is JsonValue jv)
                {
                    param.Value = jv.ToString();
                }
                param.SetModifiedCallback(_onModified ?? (() => {}));
                Parameters.Add(param);
            }
        }
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject { ["name"] = Name };

        if (Parameters.Count > 0)
        {
            var paramsObj = new JsonObject();
            foreach (var param in Parameters)
            {
                // Try to parse as number first
                if (float.TryParse(param.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var floatVal))
                {
                    paramsObj[param.Key] = floatVal;
                }
                else
                {
                    // Keep as string (expression)
                    paramsObj[param.Key] = param.Value;
                }
            }
            obj["params"] = paramsObj;
        }

        return obj;
    }

    [RelayCommand]
    private void AddParameter()
    {
        var param = new LSystemParamViewModel { Key = "param", Value = "0" };
        param.SetModifiedCallback(_onModified ?? (() => {}));
        Parameters.Add(param);
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void RemoveParameter(LSystemParamViewModel? param)
    {
        if (param != null && Parameters.Remove(param))
        {
            _onModified?.Invoke();
        }
    }
}

/// <summary>
/// A single parameter in an L-system part.
/// </summary>
public partial class LSystemParamViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "";

    private Action? _onModified;

    public void SetModifiedCallback(Action callback) => _onModified = callback;

    partial void OnKeyChanged(string value) => _onModified?.Invoke();
    partial void OnValueChanged(string value) => _onModified?.Invoke();
}
