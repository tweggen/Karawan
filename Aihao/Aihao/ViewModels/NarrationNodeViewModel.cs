using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aihao.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// ViewModel for a single node in a narration script.
/// </summary>
public partial class NarrationNodeViewModel : ObservableObject
{
    [ObservableProperty] private string _nodeId = "";
    [ObservableProperty] private string _condition = "";
    [ObservableProperty] private string _markupText = "";

    public ObservableCollection<NarrationStatementViewModel> Flow { get; } = new();

    [ObservableProperty] private NarrationStatementViewModel? _selectedStatement;

    private Action? _onModified;
    private bool _isUpdatingFromFlow;
    private bool _isUpdatingFromMarkup;

    public void SetModifiedCallback(Action callback) => _onModified = callback;

    private void MarkModified() => _onModified?.Invoke();

    /// <summary>
    /// Rebuild MarkupText from the current Flow statements.
    /// </summary>
    public void UpdateMarkupFromFlow()
    {
        if (_isUpdatingFromMarkup) return;
        _isUpdatingFromFlow = true;
        try
        {
            MarkupText = NarrationMarkupSerializer.Serialize(Flow);
        }
        finally
        {
            _isUpdatingFromFlow = false;
        }
    }

    /// <summary>
    /// Parse markup text and replace Flow statements.
    /// </summary>
    public void UpdateFlowFromMarkup(string markup)
    {
        if (_isUpdatingFromFlow) return;
        _isUpdatingFromMarkup = true;
        try
        {
            var statements = NarrationMarkupParser.Parse(markup);
            Flow.Clear();
            foreach (var s in statements)
                Flow.Add(s);
            MarkModified();
        }
        finally
        {
            _isUpdatingFromMarkup = false;
        }
    }

    /// <summary>
    /// Called after LoadFromJson to initialize MarkupText.
    /// </summary>
    public void InitializeMarkup()
    {
        UpdateMarkupFromFlow();
    }

    public void LoadFromJson(string nodeId, JsonObject obj)
    {
        NodeId = nodeId;

        if (obj.TryGetPropertyValue("condition", out var condNode))
            Condition = condNode?.GetValue<string>() ?? "";

        Flow.Clear();

        if (obj.TryGetPropertyValue("flow", out var flowNode) && flowNode is JsonArray flowArr)
        {
            foreach (var item in flowArr)
            {
                if (item is JsonObject fo)
                    Flow.Add(NarrationStatementViewModel.FromJson(fo));
            }
        }
        else
        {
            // Legacy format: synthesize flow from top-level fields
            SynthesizeFlowFromLegacy(obj);
        }
    }

    private void SynthesizeFlowFromLegacy(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("speaker", out var spk))
        {
            var stmt = new NarrationStatementViewModel { Kind = StatementKind.Speaker, Speaker = spk?.GetValue<string>() ?? "" };
            if (obj.TryGetPropertyValue("animation", out var anim))
                stmt.Animation = anim?.GetValue<string>() ?? "";
            Flow.Add(stmt);
        }

        if (obj.TryGetPropertyValue("texts", out var textsNode) && textsNode is JsonArray textsArr)
        {
            var stmt = new NarrationStatementViewModel { Kind = StatementKind.Texts };
            foreach (var t in textsArr)
            {
                if (t is JsonValue tv) stmt.Texts.Add(tv.GetValue<string>());
            }
            Flow.Add(stmt);
        }
        else if (obj.TryGetPropertyValue("text", out var textNode))
        {
            Flow.Add(new NarrationStatementViewModel { Kind = StatementKind.Text, Text = textNode?.GetValue<string>() ?? "" });
        }

        if (obj.TryGetPropertyValue("events", out var evNode) && evNode is JsonArray evArr)
        {
            var stmt = new NarrationStatementViewModel { Kind = StatementKind.Events };
            foreach (var e in evArr)
            {
                if (e is JsonObject eo) stmt.Events.Add(NarrationEventViewModel.FromJson(eo));
            }
            Flow.Add(stmt);
        }

        if (obj.TryGetPropertyValue("choices", out var chNode) && chNode is JsonArray chArr)
        {
            var stmt = new NarrationStatementViewModel { Kind = StatementKind.Choices };
            foreach (var c in chArr)
            {
                if (c is JsonObject co) stmt.Choices.Add(NarrationChoiceViewModel.FromJson(co));
            }
            Flow.Add(stmt);
        }

        if (obj.TryGetPropertyValue("goto", out var gotoNode))
        {
            var gotoObj = new JsonObject { ["goto"] = gotoNode?.DeepClone() };
            Flow.Add(NarrationStatementViewModel.FromJson(gotoObj));
        }
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject();
        if (!string.IsNullOrEmpty(Condition)) obj["condition"] = Condition;

        var flowArr = new JsonArray();
        foreach (var stmt in Flow)
            flowArr.Add(stmt.ToJson());
        obj["flow"] = flowArr;

        return obj;
    }

    [RelayCommand]
    private void AddText()
    {
        InsertMarkupAtEnd("New text...");
    }

    [RelayCommand]
    private void AddSpeaker()
    {
        InsertMarkupAtEnd("@narrator");
    }

    [RelayCommand]
    private void AddChoices()
    {
        InsertMarkupAtEnd("-# Option 1");
    }

    [RelayCommand]
    private void AddEvents()
    {
        InsertMarkupAtEnd("!event.type");
    }

    [RelayCommand]
    private void AddGoto()
    {
        InsertMarkupAtEnd("-> ");
    }

    private void InsertMarkupAtEnd(string text)
    {
        var current = MarkupText ?? "";
        if (current.Length > 0 && !current.EndsWith('\n'))
            current += "\n";
        current += "\n" + text;
        MarkupText = current;
        UpdateFlowFromMarkup(current);
    }

    [RelayCommand]
    private void RemoveStatement(NarrationStatementViewModel? stmt)
    {
        if (stmt != null && Flow.Remove(stmt))
        {
            UpdateMarkupFromFlow();
            MarkModified();
        }
    }

    [RelayCommand]
    private void MoveUp(NarrationStatementViewModel? stmt)
    {
        if (stmt == null) return;
        var idx = Flow.IndexOf(stmt);
        if (idx > 0)
        {
            Flow.Move(idx, idx - 1);
            UpdateMarkupFromFlow();
            MarkModified();
        }
    }

    [RelayCommand]
    private void MoveDown(NarrationStatementViewModel? stmt)
    {
        if (stmt == null) return;
        var idx = Flow.IndexOf(stmt);
        if (idx >= 0 && idx < Flow.Count - 1)
        {
            Flow.Move(idx, idx + 1);
            UpdateMarkupFromFlow();
            MarkModified();
        }
    }
}
