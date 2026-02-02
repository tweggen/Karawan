using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aihao.ViewModels;

public enum StatementKind
{
    Text,
    Texts,
    Choices,
    Events,
    Speaker,
    Goto
}

/// <summary>
/// ViewModel for a single choice within a Choices statement.
/// </summary>
public partial class NarrationChoiceViewModel : ObservableObject
{
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _gotoTarget = "";
    [ObservableProperty] private string _condition = "";

    public static NarrationChoiceViewModel FromJson(JsonObject obj)
    {
        var vm = new NarrationChoiceViewModel();
        if (obj.TryGetPropertyValue("text", out var t)) vm.Text = t?.GetValue<string>() ?? "";
        if (obj.TryGetPropertyValue("condition", out var c)) vm.Condition = c?.GetValue<string>() ?? "";
        if (obj.TryGetPropertyValue("goto", out var g))
        {
            // Only support simple goto in choices for now
            if (g is JsonValue jv) vm.GotoTarget = jv.GetValue<string>();
        }
        return vm;
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject { ["text"] = Text };
        if (!string.IsNullOrEmpty(GotoTarget)) obj["goto"] = GotoTarget;
        if (!string.IsNullOrEmpty(Condition)) obj["condition"] = Condition;
        return obj;
    }
}

/// <summary>
/// ViewModel for a single event descriptor within an Events statement.
/// </summary>
public partial class NarrationEventViewModel : ObservableObject
{
    [ObservableProperty] private string _type = "";
    [ObservableProperty] private string _paramsText = "";

    public static NarrationEventViewModel FromJson(JsonObject obj)
    {
        var vm = new NarrationEventViewModel();
        if (obj.TryGetPropertyValue("type", out var t)) vm.Type = t?.GetValue<string>() ?? "";
        // Collect remaining params as key=value pairs
        var parts = new List<string>();
        foreach (var kvp in obj)
        {
            if (kvp.Key == "type") continue;
            parts.Add($"{kvp.Key}={kvp.Value?.ToJsonString() ?? "null"}");
        }
        vm.ParamsText = string.Join(", ", parts);
        return vm;
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject { ["type"] = Type };
        if (!string.IsNullOrEmpty(ParamsText))
        {
            foreach (var part in ParamsText.Split(',', StringSplitOptions.TrimEntries))
            {
                var eqIdx = part.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = part[..eqIdx].Trim();
                    var val = part[(eqIdx + 1)..].Trim();
                    // Try to preserve original type
                    if (val.StartsWith('"') && val.EndsWith('"'))
                        obj[key] = JsonNode.Parse(val);
                    else
                        obj[key] = val;
                }
            }
        }
        return obj;
    }
}

/// <summary>
/// ViewModel for a single flow statement in a narration node.
/// </summary>
public partial class NarrationStatementViewModel : ObservableObject
{
    [ObservableProperty] private StatementKind _kind;
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _speaker = "";
    [ObservableProperty] private string _animation = "";
    [ObservableProperty] private string _condition = "";
    [ObservableProperty] private string _gotoTarget = "";

    /// <summary>
    /// For Texts kind: multiple random text options.
    /// </summary>
    public ObservableCollection<string> Texts { get; } = new();

    /// <summary>
    /// For Choices kind.
    /// </summary>
    public ObservableCollection<NarrationChoiceViewModel> Choices { get; } = new();

    /// <summary>
    /// For Events kind.
    /// </summary>
    public ObservableCollection<NarrationEventViewModel> Events { get; } = new();

    /// <summary>
    /// Raw goto JSON node for complex goto types (conditional/random/sequential).
    /// We preserve it for round-trip; the editor shows GotoTarget for simple gotos.
    /// </summary>
    private JsonNode? _rawGotoNode;

    public string KindLabel => Kind switch
    {
        StatementKind.Text => "Text",
        StatementKind.Texts => "Texts",
        StatementKind.Choices => "Choices",
        StatementKind.Events => "Events",
        StatementKind.Speaker => "Speaker",
        StatementKind.Goto => "Goto",
        _ => "?"
    };

    public static NarrationStatementViewModel FromJson(JsonObject obj)
    {
        var vm = new NarrationStatementViewModel();

        if (obj.TryGetPropertyValue("condition", out var cond))
            vm.Condition = cond?.GetValue<string>() ?? "";

        if (obj.TryGetPropertyValue("text", out var textNode))
        {
            vm.Kind = StatementKind.Text;
            vm.Text = textNode?.GetValue<string>() ?? "";
            return vm;
        }

        if (obj.TryGetPropertyValue("texts", out var textsNode) && textsNode is JsonArray textsArr)
        {
            vm.Kind = StatementKind.Texts;
            foreach (var item in textsArr)
            {
                if (item is JsonValue tv) vm.Texts.Add(tv.GetValue<string>());
            }
            return vm;
        }

        if (obj.TryGetPropertyValue("choices", out var choicesNode) && choicesNode is JsonArray choicesArr)
        {
            vm.Kind = StatementKind.Choices;
            foreach (var item in choicesArr)
            {
                if (item is JsonObject co) vm.Choices.Add(NarrationChoiceViewModel.FromJson(co));
            }
            return vm;
        }

        if (obj.TryGetPropertyValue("events", out var eventsNode) && eventsNode is JsonArray eventsArr)
        {
            vm.Kind = StatementKind.Events;
            foreach (var item in eventsArr)
            {
                if (item is JsonObject eo) vm.Events.Add(NarrationEventViewModel.FromJson(eo));
            }
            return vm;
        }

        if (obj.TryGetPropertyValue("speaker", out var speakerNode))
        {
            vm.Kind = StatementKind.Speaker;
            vm.Speaker = speakerNode?.GetValue<string>() ?? "";
            if (obj.TryGetPropertyValue("animation", out var animNode))
                vm.Animation = animNode?.GetValue<string>() ?? "";
            return vm;
        }

        if (obj.TryGetPropertyValue("goto", out var gotoNode))
        {
            vm.Kind = StatementKind.Goto;
            if (gotoNode is JsonValue gv)
            {
                vm.GotoTarget = gv.GetValue<string>();
            }
            else
            {
                vm._rawGotoNode = gotoNode?.DeepClone();
                vm.GotoTarget = gotoNode?.ToJsonString() ?? "";
            }
            return vm;
        }

        return vm;
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject();
        if (!string.IsNullOrEmpty(Condition)) obj["condition"] = Condition;

        switch (Kind)
        {
            case StatementKind.Text:
                obj["text"] = Text;
                break;
            case StatementKind.Texts:
                var arr = new JsonArray();
                foreach (var t in Texts) arr.Add(JsonValue.Create(t));
                obj["texts"] = arr;
                break;
            case StatementKind.Speaker:
                obj["speaker"] = Speaker;
                if (!string.IsNullOrEmpty(Animation)) obj["animation"] = Animation;
                break;
            case StatementKind.Choices:
                var ca = new JsonArray();
                foreach (var c in Choices) ca.Add(c.ToJson());
                obj["choices"] = ca;
                break;
            case StatementKind.Events:
                var ea = new JsonArray();
                foreach (var e in Events) ea.Add(e.ToJson());
                obj["events"] = ea;
                break;
            case StatementKind.Goto:
                if (_rawGotoNode != null)
                    obj["goto"] = _rawGotoNode.DeepClone();
                else
                    obj["goto"] = GotoTarget;
                break;
        }

        return obj;
    }
}
