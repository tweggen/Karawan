using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// Context for expression evaluation, providing variable lookup and built-in functions.
/// </summary>
public class ExpressionContext
{
    private readonly Params? _params;
    private readonly Random _random;
    private readonly Dictionary<string, object> _extraVariables;

    /// <summary>
    /// Access to the L-system parameters (for Lua integration).
    /// </summary>
    public Params? Parameters => _params;

    public ExpressionContext(Params? parameters, Random? random = null)
    {
        _params = parameters;
        _random = random ?? new Random();
        _extraVariables = new Dictionary<string, object>();
    }

    /// <summary>
    /// Set an additional variable in the context.
    /// </summary>
    public void SetVariable(string name, object value)
    {
        _extraVariables[name] = value;
    }

    /// <summary>
    /// Get a variable value by name.
    /// </summary>
    public object GetVariable(string name)
    {
        // Check extra variables first
        if (_extraVariables.TryGetValue(name, out var extraValue))
        {
            return extraValue;
        }

        // Check params
        if (_params?.Map != null && _params.Map.TryGetPropertyValue(name, out var jsonValue))
        {
            return JsonValueToObject(jsonValue);
        }

        throw new InvalidOperationException($"Unknown variable: ${name}");
    }

    /// <summary>
    /// Call a built-in function.
    /// </summary>
    public object CallFunction(string functionName, IReadOnlyList<ExprNode> arguments)
    {
        switch (functionName.ToLower())
        {
            case "rnd":
                if (arguments.Count == 0)
                {
                    return (float)_random.NextDouble();
                }
                else if (arguments.Count == 2)
                {
                    var min = ToFloat(arguments[0].Evaluate(this));
                    var max = ToFloat(arguments[1].Evaluate(this));
                    return min + (float)_random.NextDouble() * (max - min);
                }
                throw new ArgumentException("rnd() takes 0 or 2 arguments");

            case "sin":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Sin(ToFloat(arguments[0].Evaluate(this)));

            case "cos":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Cos(ToFloat(arguments[0].Evaluate(this)));

            case "tan":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Tan(ToFloat(arguments[0].Evaluate(this)));

            case "abs":
                RequireArgs(functionName, arguments, 1);
                return Math.Abs(ToFloat(arguments[0].Evaluate(this)));

            case "sqrt":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Sqrt(ToFloat(arguments[0].Evaluate(this)));

            case "pow":
                RequireArgs(functionName, arguments, 2);
                return (float)Math.Pow(
                    ToFloat(arguments[0].Evaluate(this)),
                    ToFloat(arguments[1].Evaluate(this)));

            case "min":
                RequireArgs(functionName, arguments, 2);
                return Math.Min(
                    ToFloat(arguments[0].Evaluate(this)),
                    ToFloat(arguments[1].Evaluate(this)));

            case "max":
                RequireArgs(functionName, arguments, 2);
                return Math.Max(
                    ToFloat(arguments[0].Evaluate(this)),
                    ToFloat(arguments[1].Evaluate(this)));

            case "clamp":
                RequireArgs(functionName, arguments, 3);
                var value = ToFloat(arguments[0].Evaluate(this));
                var clampMin = ToFloat(arguments[1].Evaluate(this));
                var clampMax = ToFloat(arguments[2].Evaluate(this));
                return Math.Clamp(value, clampMin, clampMax);

            case "lerp":
                RequireArgs(functionName, arguments, 3);
                var a = ToFloat(arguments[0].Evaluate(this));
                var b = ToFloat(arguments[1].Evaluate(this));
                var t = ToFloat(arguments[2].Evaluate(this));
                return a + (b - a) * t;

            case "floor":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Floor(ToFloat(arguments[0].Evaluate(this)));

            case "ceil":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Ceiling(ToFloat(arguments[0].Evaluate(this)));

            case "round":
                RequireArgs(functionName, arguments, 1);
                return (float)Math.Round(ToFloat(arguments[0].Evaluate(this)));

            case "deg2rad":
                RequireArgs(functionName, arguments, 1);
                return ToFloat(arguments[0].Evaluate(this)) * (float)(Math.PI / 180.0);

            case "rad2deg":
                RequireArgs(functionName, arguments, 1);
                return ToFloat(arguments[0].Evaluate(this)) * (float)(180.0 / Math.PI);

            default:
                throw new InvalidOperationException($"Unknown function: {functionName}");
        }
    }

    private static void RequireArgs(string functionName, IReadOnlyList<ExprNode> arguments, int count)
    {
        if (arguments.Count != count)
        {
            throw new ArgumentException($"{functionName}() requires {count} argument(s), got {arguments.Count}");
        }
    }

    /// <summary>
    /// Convert a JSON value to a .NET object.
    /// </summary>
    private static object JsonValueToObject(JsonNode? node)
    {
        if (node == null)
        {
            return 0f;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<float>(out var floatVal))
                return floatVal;
            if (jsonValue.TryGetValue<double>(out var doubleVal))
                return (float)doubleVal;
            if (jsonValue.TryGetValue<int>(out var intVal))
                return (float)intVal;
            if (jsonValue.TryGetValue<bool>(out var boolVal))
                return boolVal;
            if (jsonValue.TryGetValue<string>(out var strVal))
                return strVal ?? "";
        }

        // For arrays and objects, return as-is
        return node;
    }

    /// <summary>
    /// Convert any value to float.
    /// </summary>
    public static float ToFloat(object value)
    {
        return value switch
        {
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
    public static bool ToBoolean(object value)
    {
        return value switch
        {
            bool b => b,
            float f => Math.Abs(f) > 0.0001f,
            double d => Math.Abs(d) > 0.0001,
            int i => i != 0,
            long l => l != 0,
            string s => !string.IsNullOrEmpty(s) && s != "false" && s != "0",
            null => false,
            _ => true
        };
    }
}
