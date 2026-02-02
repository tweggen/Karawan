using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace engine.narration;


/// <summary>
/// Describes an event to emit when a narration node is entered.
/// </summary>
public class NarrationEventDescriptor
{
    public string Type { get; set; } = "";
    public Dictionary<string, object> Params { get; set; } = new();


    public static NarrationEventDescriptor FromJson(JsonNode node)
    {
        var desc = new NarrationEventDescriptor();
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("type", out var typeNode))
            {
                desc.Type = typeNode?.GetValue<string>() ?? "";
            }

            foreach (var kvp in obj)
            {
                if (kvp.Key == "type") continue;
                if (kvp.Value is JsonValue jv)
                {
                    desc.Params[kvp.Key] = jv.GetValueKind() switch
                    {
                        JsonValueKind.String => jv.GetValue<string>(),
                        JsonValueKind.Number => jv.GetValue<double>(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => jv.ToJsonString()
                    };
                }
            }
        }

        return desc;
    }
}


/// <summary>
/// A goto target. Supports simple (string), conditional (array),
/// weighted random (object with "random"), and sequential (object with "sequence").
/// </summary>
public class NarrationGoto
{
    public enum Kind
    {
        None,
        Simple,
        Conditional,
        Random,
        Sequential
    }

    public Kind GotoKind { get; set; } = Kind.None;

    // Simple
    public string Target { get; set; } = "";

    // Conditional: list of (condition, target) with optional else
    public List<(string Condition, string Target)> Conditionals { get; set; } = new();
    public string ElseTarget { get; set; } = "";

    // Random
    public List<(float Weight, string Target)> RandomEntries { get; set; } = new();

    // Sequential
    public List<string> Sequence { get; set; } = new();
    public string Overflow { get; set; } = "cycle"; // "cycle", "clamp", "random"


    public static NarrationGoto FromJson(JsonNode node)
    {
        if (node is null)
        {
            return new NarrationGoto { GotoKind = Kind.None };
        }

        // Simple string
        if (node is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
        {
            return new NarrationGoto
            {
                GotoKind = Kind.Simple,
                Target = jv.GetValue<string>()
            };
        }

        // Conditional: array of { "if": ..., "goto": ... } or { "else": ... }
        if (node is JsonArray arr)
        {
            var g = new NarrationGoto { GotoKind = Kind.Conditional };
            foreach (var item in arr)
            {
                if (item is JsonObject condObj)
                {
                    if (condObj.TryGetPropertyValue("else", out var elseNode))
                    {
                        g.ElseTarget = elseNode?.GetValue<string>() ?? "";
                    }
                    else if (condObj.TryGetPropertyValue("if", out var ifNode)
                             && condObj.TryGetPropertyValue("goto", out var gotoNode))
                    {
                        g.Conditionals.Add((
                            ifNode?.GetValue<string>() ?? "",
                            gotoNode?.GetValue<string>() ?? ""));
                    }
                }
            }

            return g;
        }

        // Object with "random" or "sequence"
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("random", out var randomNode) && randomNode is JsonArray randomArr)
            {
                var g = new NarrationGoto { GotoKind = Kind.Random };
                foreach (var entry in randomArr)
                {
                    if (entry is JsonObject entryObj)
                    {
                        float weight = 1f;
                        string target = "";
                        if (entryObj.TryGetPropertyValue("weight", out var wNode))
                        {
                            weight = wNode?.GetValue<float>() ?? 1f;
                        }

                        if (entryObj.TryGetPropertyValue("goto", out var tNode))
                        {
                            target = tNode?.GetValue<string>() ?? "";
                        }

                        g.RandomEntries.Add((weight, target));
                    }
                }

                return g;
            }

            if (obj.TryGetPropertyValue("sequence", out var seqNode) && seqNode is JsonArray seqArr)
            {
                var g = new NarrationGoto { GotoKind = Kind.Sequential };
                foreach (var entry in seqArr)
                {
                    if (entry is JsonValue sv && sv.GetValueKind() == JsonValueKind.String)
                    {
                        g.Sequence.Add(sv.GetValue<string>());
                    }
                }

                if (obj.TryGetPropertyValue("overflow", out var overflowNode))
                {
                    g.Overflow = overflowNode?.GetValue<string>() ?? "cycle";
                }

                return g;
            }
        }

        Warning($"NarrationGoto: unable to parse goto node: {node.ToJsonString()}");
        return new NarrationGoto { GotoKind = Kind.None };
    }
}


/// <summary>
/// A single choice option within a narration node.
/// </summary>
public class NarrationChoice
{
    public string Text { get; set; } = "";
    public NarrationGoto Goto { get; set; } = new();
    public string Condition { get; set; } = "";


    public static NarrationChoice FromJson(JsonNode node)
    {
        var choice = new NarrationChoice();
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("text", out var textNode))
            {
                choice.Text = textNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("goto", out var gotoNode))
            {
                choice.Goto = NarrationGoto.FromJson(gotoNode);
            }

            if (obj.TryGetPropertyValue("condition", out var condNode))
            {
                choice.Condition = condNode?.GetValue<string>() ?? "";
            }
        }

        return choice;
    }
}


/// <summary>
/// A single statement within a node's flow.
/// </summary>
public class NarrationStatement
{
    public enum Kind { Text, Choices, Events, Speaker, Goto }

    public Kind StatementKind { get; set; }
    public string Text { get; set; } = "";
    public List<string> Texts { get; set; } = new();
    public string Speaker { get; set; } = "";
    public string Animation { get; set; } = "";
    public List<NarrationChoice> Choices { get; set; } = new();
    public List<NarrationEventDescriptor> Events { get; set; } = new();
    public NarrationGoto Goto { get; set; } = new();
    public string Condition { get; set; } = "";


    public string ResolveText(Random rng)
    {
        if (Texts.Count > 0)
        {
            return Texts[rng.Next(Texts.Count)];
        }
        return Text;
    }


    public static NarrationStatement FromJson(JsonNode node)
    {
        if (node is not JsonObject obj) return null;

        var stmt = new NarrationStatement();

        if (obj.TryGetPropertyValue("condition", out var condNode))
        {
            stmt.Condition = condNode?.GetValue<string>() ?? "";
        }

        if (obj.TryGetPropertyValue("text", out var textNode))
        {
            stmt.StatementKind = Kind.Text;
            stmt.Text = textNode?.GetValue<string>() ?? "";
            return stmt;
        }

        if (obj.TryGetPropertyValue("texts", out var textsNode) && textsNode is JsonArray textsArr)
        {
            stmt.StatementKind = Kind.Text;
            foreach (var item in textsArr)
            {
                if (item is JsonValue tv && tv.GetValueKind() == JsonValueKind.String)
                {
                    stmt.Texts.Add(tv.GetValue<string>());
                }
            }
            return stmt;
        }

        if (obj.TryGetPropertyValue("choices", out var choicesNode) && choicesNode is JsonArray choicesArr)
        {
            stmt.StatementKind = Kind.Choices;
            foreach (var choiceItem in choicesArr)
            {
                stmt.Choices.Add(NarrationChoice.FromJson(choiceItem));
            }
            return stmt;
        }

        if (obj.TryGetPropertyValue("events", out var eventsNode) && eventsNode is JsonArray eventsArr)
        {
            stmt.StatementKind = Kind.Events;
            foreach (var eventItem in eventsArr)
            {
                stmt.Events.Add(NarrationEventDescriptor.FromJson(eventItem));
            }
            return stmt;
        }

        if (obj.TryGetPropertyValue("speaker", out var speakerNode))
        {
            stmt.StatementKind = Kind.Speaker;
            stmt.Speaker = speakerNode?.GetValue<string>() ?? "";
            if (obj.TryGetPropertyValue("animation", out var animNode))
            {
                stmt.Animation = animNode?.GetValue<string>() ?? "";
            }
            return stmt;
        }

        if (obj.TryGetPropertyValue("goto", out var gotoNode))
        {
            stmt.StatementKind = Kind.Goto;
            stmt.Goto = NarrationGoto.FromJson(gotoNode);
            return stmt;
        }

        Warning($"NarrationStatement: unable to parse statement: {node.ToJsonString()}");
        return null;
    }
}


/// <summary>
/// A single node (passage) in a narration script.
/// </summary>
public class NarrationNode
{
    public string Text { get; set; } = "";

    /// <summary>
    /// If set, one text is picked at random each time the node is entered.
    /// Takes precedence over Text.
    /// </summary>
    public List<string> Texts { get; set; } = new();

    public string Speaker { get; set; } = "";
    public string Animation { get; set; } = "";
    public List<NarrationChoice> Choices { get; set; } = new();
    public NarrationGoto Goto { get; set; } = new();
    public List<NarrationEventDescriptor> Events { get; set; } = new();
    public string Condition { get; set; } = "";

    /// <summary>
    /// Ordered flow of statements. When present, the runner steps through
    /// these one at a time. When absent, synthesized from legacy fields.
    /// </summary>
    public List<NarrationStatement> Flow { get; set; } = new();


    /// <summary>
    /// Resolve the effective text for this node. If Texts is non-empty,
    /// pick one at random; otherwise return Text.
    /// </summary>
    public string ResolveText(Random rng)
    {
        if (Texts.Count > 0)
        {
            return Texts[rng.Next(Texts.Count)];
        }

        return Text;
    }


    /// <summary>
    /// Synthesize a flow from legacy fields when no explicit flow is provided.
    /// </summary>
    private void _synthesizeFlow()
    {
        if (!string.IsNullOrEmpty(Speaker))
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Speaker,
                Speaker = Speaker,
                Animation = Animation
            });
        }

        if (Texts.Count > 0)
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Text,
                Texts = new List<string>(Texts)
            });
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Text,
                Text = Text
            });
        }

        if (Events.Count > 0)
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Events,
                Events = new List<NarrationEventDescriptor>(Events)
            });
        }

        if (Choices.Count > 0)
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Choices,
                Choices = new List<NarrationChoice>(Choices)
            });
        }

        if (Goto.GotoKind != NarrationGoto.Kind.None)
        {
            Flow.Add(new NarrationStatement
            {
                StatementKind = NarrationStatement.Kind.Goto,
                Goto = Goto
            });
        }
    }


    public static NarrationNode FromJson(JsonNode node)
    {
        var n = new NarrationNode();
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("text", out var textNode))
            {
                n.Text = textNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("texts", out var textsNode) && textsNode is JsonArray textsArr)
            {
                foreach (var item in textsArr)
                {
                    if (item is JsonValue tv && tv.GetValueKind() == JsonValueKind.String)
                    {
                        n.Texts.Add(tv.GetValue<string>());
                    }
                }
            }

            if (obj.TryGetPropertyValue("speaker", out var speakerNode))
            {
                n.Speaker = speakerNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("animation", out var animNode))
            {
                n.Animation = animNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("condition", out var condNode))
            {
                n.Condition = condNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("goto", out var gotoNode))
            {
                n.Goto = NarrationGoto.FromJson(gotoNode);
            }

            if (obj.TryGetPropertyValue("choices", out var choicesNode) && choicesNode is JsonArray choicesArr)
            {
                foreach (var choiceItem in choicesArr)
                {
                    n.Choices.Add(NarrationChoice.FromJson(choiceItem));
                }
            }

            if (obj.TryGetPropertyValue("events", out var eventsNode) && eventsNode is JsonArray eventsArr)
            {
                foreach (var eventItem in eventsArr)
                {
                    n.Events.Add(NarrationEventDescriptor.FromJson(eventItem));
                }
            }

            // Parse explicit flow array, or synthesize from legacy fields
            if (obj.TryGetPropertyValue("flow", out var flowNode) && flowNode is JsonArray flowArr)
            {
                foreach (var flowItem in flowArr)
                {
                    var stmt = NarrationStatement.FromJson(flowItem);
                    if (stmt != null)
                    {
                        n.Flow.Add(stmt);
                    }
                }
            }
            else
            {
                n._synthesizeFlow();
            }
        }

        return n;
    }
}


/// <summary>
/// A narration script: a named collection of nodes forming a conversation tree.
/// </summary>
public class NarrationScript
{
    public string Name { get; set; } = "";
    public string StartNodeId { get; set; } = "";
    public Dictionary<string, NarrationNode> Nodes { get; set; } = new();


    public static NarrationScript FromJson(string name, JsonNode node)
    {
        var script = new NarrationScript { Name = name };
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("start", out var startNode))
            {
                script.StartNodeId = startNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("nodes", out var nodesNode) && nodesNode is JsonObject nodesObj)
            {
                foreach (var kvp in nodesObj)
                {
                    script.Nodes[kvp.Key] = NarrationNode.FromJson(kvp.Value);
                }
            }
        }

        return script;
    }
}


/// <summary>
/// A trigger binding: maps an event path to a script activation.
/// </summary>
public class NarrationTrigger
{
    public string EventPath { get; set; } = "";
    public string ScriptName { get; set; } = "";
    public string Mode { get; set; } = "conversation"; // "conversation", "narration", "scriptedScene"


    public static NarrationTrigger FromJson(string eventPath, JsonNode node)
    {
        var trigger = new NarrationTrigger { EventPath = eventPath };
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("script", out var scriptNode))
            {
                trigger.ScriptName = scriptNode?.GetValue<string>() ?? "";
            }

            if (obj.TryGetPropertyValue("mode", out var modeNode))
            {
                trigger.Mode = modeNode?.GetValue<string>() ?? "conversation";
            }
        }

        return trigger;
    }
}
