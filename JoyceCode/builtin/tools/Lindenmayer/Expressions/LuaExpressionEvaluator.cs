using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using engine.gongzuo;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// Evaluates Lua expressions for L-system definitions.
/// Expressions prefixed with "lua:" are evaluated using the Lua engine.
/// </summary>
public class LuaExpressionEvaluator : IDisposable
{
    private readonly CompiledCache _cache;
    private readonly Random _random;
    private bool _isDisposed;

    /// <summary>
    /// Lua prefix that indicates an expression should be evaluated by Lua.
    /// </summary>
    public const string LuaPrefix = "lua:";

    public LuaExpressionEvaluator(Random? random = null)
    {
        _cache = new CompiledCache();
        _random = random ?? new Random();
    }

    /// <summary>
    /// Check if an expression is a Lua expression.
    /// </summary>
    public static bool IsLuaExpression(string expression)
    {
        return expression != null && expression.StartsWith(LuaPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract the Lua code from a lua: prefixed expression.
    /// </summary>
    public static string ExtractLuaCode(string expression)
    {
        if (IsLuaExpression(expression))
        {
            return expression.Substring(LuaPrefix.Length).Trim();
        }
        return expression;
    }

    /// <summary>
    /// Evaluate a Lua expression with the given L-system parameters.
    /// </summary>
    public object Evaluate(string expression, Params? parameters)
    {
        if (!IsLuaExpression(expression))
        {
            throw new ArgumentException($"Expression must start with '{LuaPrefix}'", nameof(expression));
        }

        string luaCode = ExtractLuaCode(expression);

        // Wrap the expression in a return statement if it doesn't have one
        string script = luaCode.Contains("return") ? luaCode : $"return {luaCode}";

        // Get or create the compiled script entry
        var lse = _cache.Find(expression, script, entry => SetupBindings(entry, parameters));

        // Update parameter bindings before each call
        UpdateParameterBindings(lse, parameters);

        // Execute and return result
        var result = lse.CallSingleResult();
        return result ?? 0f;
    }

    /// <summary>
    /// Evaluate a Lua expression, returning a float result.
    /// </summary>
    public float EvaluateFloat(string expression, Params? parameters)
    {
        var result = Evaluate(expression, parameters);
        return ToFloat(result);
    }

    /// <summary>
    /// Evaluate a Lua expression, returning a boolean result.
    /// </summary>
    public bool EvaluateBool(string expression, Params? parameters)
    {
        var result = Evaluate(expression, parameters);
        return ToBoolean(result);
    }

    /// <summary>
    /// Set up initial bindings for a Lua script entry.
    /// </summary>
    private void SetupBindings(LuaScriptEntry entry, Params? parameters)
    {
        // Create binding frame with built-in functions
        var bindings = new Dictionary<string, object>
        {
            // Math functions
            ["sin"] = new Func<double, double>(Math.Sin),
            ["cos"] = new Func<double, double>(Math.Cos),
            ["tan"] = new Func<double, double>(Math.Tan),
            ["abs"] = new Func<double, double>(Math.Abs),
            ["sqrt"] = new Func<double, double>(Math.Sqrt),
            ["pow"] = new Func<double, double, double>(Math.Pow),
            ["min"] = new Func<double, double, double>(Math.Min),
            ["max"] = new Func<double, double, double>(Math.Max),
            ["floor"] = new Func<double, double>(Math.Floor),
            ["ceil"] = new Func<double, double>(Math.Ceiling),
            ["round"] = new Func<double, double>(Math.Round),

            // Random function
            ["rnd"] = new Func<double>(() => _random.NextDouble()),
            ["rnd2"] = new Func<double, double, double>((min, max) => min + _random.NextDouble() * (max - min)),

            // Conversion helpers
            ["deg2rad"] = new Func<double, double>(d => d * Math.PI / 180.0),
            ["rad2deg"] = new Func<double, double>(r => r * 180.0 / Math.PI),

            // Clamp and lerp
            ["clamp"] = new Func<double, double, double, double>((v, lo, hi) => Math.Max(lo, Math.Min(hi, v))),
            ["lerp"] = new Func<double, double, double, double>((a, b, t) => a + (b - a) * t),
        };

        // Create a parameter table 'p' for accessing L-system parameters
        // Parameters will be updated before each call
        bindings["p"] = CreateParameterTable(parameters);

        var frame = new LuaBindingFrame { MapBindings = bindings };
        entry.PushBinding(frame);
    }

    /// <summary>
    /// Update parameter bindings before each evaluation.
    /// </summary>
    private void UpdateParameterBindings(LuaScriptEntry entry, Params? parameters)
    {
        if (entry.LuaState != null && parameters?.Map != null)
        {
            // Update the 'p' table with current parameter values
            var pTable = CreateParameterTable(parameters);
            entry.LuaState["p"] = pTable;

            // Also set individual variables for convenience (r, l, h, etc.)
            foreach (var prop in parameters.Map)
            {
                if (prop.Value != null)
                {
                    entry.LuaState[prop.Key] = JsonNodeToObject(prop.Value);
                }
            }
        }
    }

    /// <summary>
    /// Create a dictionary from L-system parameters for Lua access.
    /// </summary>
    private Dictionary<string, object> CreateParameterTable(Params? parameters)
    {
        var table = new Dictionary<string, object>();

        if (parameters?.Map != null)
        {
            foreach (var prop in parameters.Map)
            {
                if (prop.Value != null)
                {
                    table[prop.Key] = JsonNodeToObject(prop.Value);
                }
            }
        }

        return table;
    }

    /// <summary>
    /// Convert a JsonNode to a .NET object for Lua.
    /// </summary>
    private static object JsonNodeToObject(JsonNode? node)
    {
        if (node == null) return 0.0;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out var doubleVal))
                return doubleVal;
            if (jsonValue.TryGetValue<float>(out var floatVal))
                return (double)floatVal;
            if (jsonValue.TryGetValue<int>(out var intVal))
                return (double)intVal;
            if (jsonValue.TryGetValue<long>(out var longVal))
                return (double)longVal;
            if (jsonValue.TryGetValue<bool>(out var boolVal))
                return boolVal;
            if (jsonValue.TryGetValue<string>(out var strVal))
                return strVal ?? "";
        }

        return node.ToJsonString();
    }

    /// <summary>
    /// Convert any value to float.
    /// </summary>
    public static float ToFloat(object? value)
    {
        return value switch
        {
            null => 0f,
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            bool b => b ? 1f : 0f,
            string s => float.TryParse(s, out var parsed) ? parsed : 0f,
            _ => 0f
        };
    }

    /// <summary>
    /// Convert any value to boolean.
    /// </summary>
    public static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            float f => Math.Abs(f) > 0.0001f,
            double d => Math.Abs(d) > 0.0001,
            int i => i != 0,
            long l => l != 0,
            string s => !string.IsNullOrEmpty(s) && s != "false" && s != "0",
            _ => true
        };
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
}
