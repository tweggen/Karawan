using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace engine.streets.generation;


/**
 * Parses a street generation ruleset out of the Mix configuration.
 *
 * Deliberately a plain function of a JsonNode: it does not reach into the I container,
 * so the deterministic test harness can drive it without booting an engine, and no
 * parsing happens anywhere near the generation loop. Everything is resolved into the
 * compiled ExpansionRuleTable once, before a cluster is grown.
 *
 * Anything the parser does not recognise is an error at parse time. A ruleset that
 * silently ignored a misspelled field would change how a city looks and give no clue
 * why, which is the failure mode this whole rework exists to prevent.
 */
internal static class StreetGenConfig
{
    /**
     * @param node
     *     The /streetGen subtree, or null when the game ships no ruleset.
     * @returns
     *     The parsed table, or the built-in defaults when node is null.
     */
    internal static ExpansionRuleTable Parse(JsonNode node)
    {
        if (null == node)
        {
            return ExpansionRuleTable.Defaults();
        }

        JsonObject root = node as JsonObject;
        if (null == root)
        {
            throw new InvalidOperationException(
                $"streetGen must be an object, found {node.GetType().Name}.");
        }

        var groupsNode = _property(root, "weightGroups") as JsonArray;
        if (null == groupsNode)
        {
            throw new InvalidOperationException("streetGen.weightGroups is missing or not an array.");
        }

        var groups = new List<WeightGroupSpec>();
        var groupIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in groupsNode)
        {
            var g = entry as JsonObject;
            if (null == g)
            {
                throw new InvalidOperationException("streetGen.weightGroups contains a non-object.");
            }

            string name = _requiredString(g, "name", "streetGen.weightGroups");
            if (groupIndexByName.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"streetGen.weightGroups defines '{name}' more than once.");
            }

            groupIndexByName[name] = groups.Count;
            groups.Add(new WeightGroupSpec(
                name,
                _requiredFloat(g, "decreaseProbability", $"streetGen.weightGroups.{name}"),
                _requiredFloat(g, "increaseProbability", $"streetGen.weightGroups.{name}")));
        }

        var rulesNode = _property(root, "rules") as JsonArray;
        if (null == rulesNode)
        {
            throw new InvalidOperationException("streetGen.rules is missing or not an array.");
        }

        var rules = new List<ExpansionRule>();

        foreach (var entry in rulesNode)
        {
            var r = entry as JsonObject;
            if (null == r)
            {
                throw new InvalidOperationException("streetGen.rules contains a non-object.");
            }

            string name = _requiredString(r, "name", "streetGen.rules");
            string where = $"streetGen.rules.{name}";

            string groupName = _requiredString(r, "weightGroup", where);
            if (!groupIndexByName.TryGetValue(groupName, out int groupIndex))
            {
                throw new InvalidOperationException(
                    $"{where} names weight group '{groupName}', which is not defined in " +
                    $"streetGen.weightGroups.");
            }

            rules.Add(new ExpansionRule(
                name,
                _direction(_requiredString(r, "direction", where), where),
                groupIndex,
                _probability(_property(r, "probability") as JsonObject, where),
                keepPrimary: _requiredBool(r, "keepPrimary", where),
                isFallback: _optionalBool(r, "isFallback")));
        }

        /*
         * The table constructor performs the remaining structural checks: at least one
         * rule and group, exactly one fallback, group indices in range.
         */
        return new ExpansionRuleTable(rules, groups);
    }


    private static StrokeDirection _direction(string value, string where)
    {
        switch (value.ToLowerInvariant())
        {
            case "forward": return StrokeDirection.Forward;
            case "right": return StrokeDirection.Right;
            case "left": return StrokeDirection.Left;
            case "random": return StrokeDirection.Random;
            default:
                throw new InvalidOperationException(
                    $"{where} has direction '{value}'. Expected forward, right, left or random.");
        }
    }


    private static ProbExpr _probability(JsonObject node, string where)
    {
        if (null == node)
        {
            throw new InvalidOperationException($"{where} has no probability object.");
        }

        string kind = _requiredString(node, "kind", where).ToLowerInvariant();

        switch (kind)
        {
            case "constant":
                return ProbExpr.Constant(_requiredFloat(node, "a", where));

            case "affine":
                return ProbExpr.Affine(
                    _requiredFloat(node, "a", where), _requiredFloat(node, "b", where));

            case "hyperbola":
                return ProbExpr.Hyperbola(
                    _requiredFloat(node, "a", where), _requiredFloat(node, "b", where));

            default:
                throw new InvalidOperationException(
                    $"{where} has probability kind '{kind}'. Only constant, affine and " +
                    $"hyperbola are supported; these are the shapes the generator has " +
                    $"always used, and an unrecognised one is refused here rather than " +
                    $"silently drawn against later.");
        }
    }


    /**
     * Case-insensitive lookup, matching the house style for configuration JSON.
     */
    private static JsonNode _property(JsonObject obj, string name)
    {
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return null;
    }


    private static string _requiredString(JsonObject obj, string name, string where)
    {
        var node = _property(obj, name);
        if (null == node)
        {
            throw new InvalidOperationException($"{where} is missing '{name}'.");
        }

        string value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{where} has an empty '{name}'.");
        }

        return value;
    }


    private static float _requiredFloat(JsonObject obj, string name, string where)
    {
        var node = _property(obj, name);
        if (null == node)
        {
            throw new InvalidOperationException($"{where} is missing '{name}'.");
        }

        return node.GetValue<float>();
    }


    private static bool _requiredBool(JsonObject obj, string name, string where)
    {
        var node = _property(obj, name);
        if (null == node)
        {
            throw new InvalidOperationException($"{where} is missing '{name}'.");
        }

        return node.GetValue<bool>();
    }


    private static bool _optionalBool(JsonObject obj, string name)
    {
        var node = _property(obj, name);
        return null != node && node.GetValue<bool>();
    }
}
