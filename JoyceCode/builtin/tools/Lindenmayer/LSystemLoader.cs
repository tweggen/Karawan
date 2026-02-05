using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.tools.Lindenmayer.Expressions;

namespace builtin.tools.Lindenmayer;

/// <summary>
/// Loads L-system definitions from JSON and creates runtime System objects.
/// </summary>
public class LSystemLoader
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly Random _random;

    public LSystemLoader(Random? random = null)
    {
        _evaluator = new ExpressionEvaluator(enableCache: true);
        _random = random ?? new Random();
    }

    /// <summary>
    /// Load an L-system catalog from a JSON string.
    /// </summary>
    public LSystemCatalog LoadCatalog(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonSerializer.Deserialize<LSystemCatalog>(json, options)
               ?? new LSystemCatalog();
    }

    /// <summary>
    /// Load a single L-system definition from a JSON string.
    /// </summary>
    public LSystemDefinition LoadDefinition(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonSerializer.Deserialize<LSystemDefinition>(json, options)
               ?? new LSystemDefinition();
    }

    /// <summary>
    /// Create a runtime System from a definition.
    /// </summary>
    public System CreateSystem(LSystemDefinition definition)
    {
        // Create seed state
        var seedParts = CreateSeedParts(definition.Seed);
        var seed = new State(seedParts);

        // Create rules
        var rules = definition.Rules.Select(CreateRule).ToList();

        // Create macros
        var macros = definition.Macros.Select(CreateRule).ToList();

        return new System(seed, rules, macros);
    }

    /// <summary>
    /// Create seed parts, evaluating any expressions in the initial state.
    /// </summary>
    private List<Part> CreateSeedParts(SeedDefinition seedDef)
    {
        var parts = new List<Part>();
        var context = new ExpressionContext(null, _random);

        foreach (var partDef in seedDef.Parts)
        {
            var jsonParams = EvaluateParams(partDef.Params, context);
            parts.Add(new Part(partDef.Name, jsonParams));
        }

        return parts;
    }

    /// <summary>
    /// Create a runtime Rule from a RuleDefinition.
    /// </summary>
    private Rule CreateRule(RuleDefinition ruleDef)
    {
        // Parse condition expression (if any)
        ExprNode? conditionAst = null;
        if (!string.IsNullOrWhiteSpace(ruleDef.Condition))
        {
            conditionAst = _evaluator.Parse(ruleDef.Condition);
        }

        // Parse all transform part parameter expressions
        var transformParts = ParseTransformParts(ruleDef.Transform);

        // Create condition function
        Func<Params, bool> condition;
        if (conditionAst != null)
        {
            condition = (Params p) =>
            {
                var context = new ExpressionContext(p, _random);
                var result = conditionAst.Evaluate(context);
                return ExpressionContext.ToBoolean(result);
            };
        }
        else
        {
            condition = Rule.Always;
        }

        // Create transform function
        Func<Params, IList<Part>> transformFunc = (Params p) =>
        {
            var context = new ExpressionContext(p, _random);
            var result = new List<Part>();

            foreach (var (name, paramExprs) in transformParts)
            {
                var jsonParams = EvaluateParamExpressions(paramExprs, context);
                result.Add(new Part(name, jsonParams));
            }

            return result;
        };

        return new Rule(ruleDef.Match, ruleDef.Probability, condition, transformFunc);
    }

    /// <summary>
    /// Pre-parse all parameter expressions in transform parts.
    /// Returns list of (partName, parameterExpressions).
    /// </summary>
    private List<(string Name, Dictionary<string, ParamValue> Params)> ParseTransformParts(
        List<PartDefinition> partDefs)
    {
        var result = new List<(string, Dictionary<string, ParamValue>)>();

        foreach (var partDef in partDefs)
        {
            var paramExprs = new Dictionary<string, ParamValue>();

            if (partDef.Params != null)
            {
                foreach (var (key, value) in partDef.Params)
                {
                    paramExprs[key] = ParseParamValue(value);
                }
            }

            result.Add((partDef.Name, paramExprs));
        }

        return result;
    }

    /// <summary>
    /// Parse a parameter value - could be a literal or an expression string.
    /// </summary>
    private ParamValue ParseParamValue(object? value)
    {
        if (value == null)
        {
            return new ParamValue(0f);
        }

        // Handle JsonElement from deserialization
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => new ParamValue(element.GetSingle()),
                JsonValueKind.True => new ParamValue(true),
                JsonValueKind.False => new ParamValue(false),
                JsonValueKind.String => ParseStringValue(element.GetString() ?? ""),
                _ => new ParamValue(0f)
            };
        }

        // Handle direct values
        if (value is float f)
            return new ParamValue(f);
        if (value is double d)
            return new ParamValue((float)d);
        if (value is int i)
            return new ParamValue((float)i);
        if (value is long l)
            return new ParamValue((float)l);
        if (value is bool b)
            return new ParamValue(b);
        if (value is string s)
            return ParseStringValue(s);

        return new ParamValue(0f);
    }

    /// <summary>
    /// Parse a string value - detect if it's an expression or a literal.
    /// </summary>
    private ParamValue ParseStringValue(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return new ParamValue(0f);

        // Check if it looks like an expression
        if (ExpressionEvaluator.IsExpression(s))
        {
            var ast = _evaluator.Parse(s);
            return new ParamValue(ast);
        }

        // Try to parse as a number literal
        if (float.TryParse(s, global::System.Globalization.NumberStyles.Float,
            global::System.Globalization.CultureInfo.InvariantCulture, out var floatVal))
        {
            return new ParamValue(floatVal);
        }

        // Return as string
        return new ParamValue(s);
    }

    /// <summary>
    /// Evaluate literal params (no expressions, used for seed).
    /// </summary>
    private JsonObject? EvaluateParams(Dictionary<string, object>? paramsDef, ExpressionContext context)
    {
        if (paramsDef == null || paramsDef.Count == 0)
            return null;

        var result = new JsonObject();

        foreach (var (key, value) in paramsDef)
        {
            var paramValue = ParseParamValue(value);
            result[key] = EvaluateParamValue(paramValue, context);
        }

        return result;
    }

    /// <summary>
    /// Evaluate pre-parsed parameter expressions.
    /// </summary>
    private JsonObject? EvaluateParamExpressions(Dictionary<string, ParamValue> paramExprs,
        ExpressionContext context)
    {
        if (paramExprs.Count == 0)
            return null;

        var result = new JsonObject();

        foreach (var (key, paramValue) in paramExprs)
        {
            result[key] = EvaluateParamValue(paramValue, context);
        }

        return result;
    }

    /// <summary>
    /// Evaluate a single parameter value.
    /// </summary>
    private JsonNode EvaluateParamValue(ParamValue paramValue, ExpressionContext context)
    {
        if (paramValue.Expression != null)
        {
            var result = paramValue.Expression.Evaluate(context);
            return JsonValue.Create(ExpressionContext.ToFloat(result));
        }

        if (paramValue.LiteralFloat.HasValue)
            return JsonValue.Create(paramValue.LiteralFloat.Value);

        if (paramValue.LiteralBool.HasValue)
            return JsonValue.Create(paramValue.LiteralBool.Value);

        if (paramValue.LiteralString != null)
            return JsonValue.Create(paramValue.LiteralString);

        return JsonValue.Create(0f);
    }

    /// <summary>
    /// Represents a parameter value that can be a literal or an expression.
    /// </summary>
    private readonly struct ParamValue
    {
        public ExprNode? Expression { get; }
        public float? LiteralFloat { get; }
        public bool? LiteralBool { get; }
        public string? LiteralString { get; }

        public ParamValue(ExprNode expression)
        {
            Expression = expression;
            LiteralFloat = null;
            LiteralBool = null;
            LiteralString = null;
        }

        public ParamValue(float value)
        {
            Expression = null;
            LiteralFloat = value;
            LiteralBool = null;
            LiteralString = null;
        }

        public ParamValue(bool value)
        {
            Expression = null;
            LiteralFloat = null;
            LiteralBool = value;
            LiteralString = null;
        }

        public ParamValue(string value)
        {
            Expression = null;
            LiteralFloat = null;
            LiteralBool = null;
            LiteralString = value;
        }
    }
}
