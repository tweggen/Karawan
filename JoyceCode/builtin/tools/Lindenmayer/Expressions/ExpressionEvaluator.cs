using System;
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// High-level API for parsing and evaluating expression strings.
/// Supports both simple expressions and Lua expressions (prefixed with "lua:").
/// </summary>
public class ExpressionEvaluator : IDisposable
{
    private readonly ExpressionParser _parser;
    private readonly Dictionary<string, ExprNode> _cache;
    private readonly bool _enableCache;
    private readonly Random _random;
    private LuaExpressionEvaluator? _luaEvaluator;
    private bool _isDisposed;

    public ExpressionEvaluator(bool enableCache = true, Random? random = null)
    {
        _parser = new ExpressionParser();
        _cache = new Dictionary<string, ExprNode>();
        _enableCache = enableCache;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Get or create the Lua evaluator (lazy initialization).
    /// </summary>
    private LuaExpressionEvaluator LuaEvaluator
    {
        get
        {
            if (_luaEvaluator == null)
            {
                _luaEvaluator = new LuaExpressionEvaluator(_random);
            }
            return _luaEvaluator;
        }
    }

    /// <summary>
    /// Parse and evaluate an expression string with the given context.
    /// Supports both simple expressions and lua: prefixed Lua expressions.
    /// </summary>
    public object Evaluate(string expression, ExpressionContext context)
    {
        // Check for Lua expression
        if (LuaExpressionEvaluator.IsLuaExpression(expression))
        {
            return LuaEvaluator.Evaluate(expression, context.Parameters);
        }

        var ast = Parse(expression);
        return ast.Evaluate(context);
    }

    /// <summary>
    /// Parse and evaluate an expression, returning a float result.
    /// </summary>
    public float EvaluateFloat(string expression, ExpressionContext context)
    {
        // Check for Lua expression
        if (LuaExpressionEvaluator.IsLuaExpression(expression))
        {
            return LuaEvaluator.EvaluateFloat(expression, context.Parameters);
        }

        var result = Evaluate(expression, context);
        return ExpressionContext.ToFloat(result);
    }

    /// <summary>
    /// Parse and evaluate an expression, returning a boolean result.
    /// </summary>
    public bool EvaluateBool(string expression, ExpressionContext context)
    {
        // Check for Lua expression
        if (LuaExpressionEvaluator.IsLuaExpression(expression))
        {
            return LuaEvaluator.EvaluateBool(expression, context.Parameters);
        }

        var result = Evaluate(expression, context);
        return ExpressionContext.ToBoolean(result);
    }

    /// <summary>
    /// Parse an expression string into an AST (with optional caching).
    /// </summary>
    public ExprNode Parse(string expression)
    {
        if (_enableCache && _cache.TryGetValue(expression, out var cached))
        {
            return cached;
        }

        var ast = _parser.Parse(expression);

        if (_enableCache)
        {
            _cache[expression] = ast;
        }

        return ast;
    }

    /// <summary>
    /// Clear the expression cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Check if a string looks like it contains an expression (has operators or variables).
    /// Simple literals (just a number or string) return false.
    /// Also returns true for lua: prefixed expressions.
    /// </summary>
    public static bool IsExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Lua expression
        if (LuaExpressionEvaluator.IsLuaExpression(value))
            return true;

        // Contains variable reference
        if (value.Contains('$'))
            return true;

        // Contains operators
        if (value.Contains('+') || value.Contains('-') || value.Contains('*') ||
            value.Contains('/') || value.Contains('%') || value.Contains('>') ||
            value.Contains('<') || value.Contains('=') || value.Contains('!') ||
            value.Contains('&') || value.Contains('|') || value.Contains('?'))
        {
            return true;
        }

        // Contains function call (identifier followed by parenthesis)
        if (value.Contains('('))
            return true;

        return false;
    }

    /// <summary>
    /// Try to parse a value that could be either a literal or an expression.
    /// Returns the literal value directly if possible, otherwise parses as expression.
    /// </summary>
    public object EvaluateOrLiteral(string value, ExpressionContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        // Try to parse as a simple literal first
        if (!IsExpression(value))
        {
            // Try float
            if (float.TryParse(value, global::System.Globalization.NumberStyles.Float,
                global::System.Globalization.CultureInfo.InvariantCulture, out var floatVal))
            {
                return floatVal;
            }

            // Try bool
            if (bool.TryParse(value, out var boolVal))
            {
                return boolVal;
            }

            // Return as string (could be a bare identifier used as string)
            return value;
        }

        // Parse and evaluate as expression
        return Evaluate(value, context);
    }

    /// <summary>
    /// Convenience method: evaluate or return literal as float.
    /// </summary>
    public float EvaluateOrLiteralFloat(string value, ExpressionContext context)
    {
        var result = EvaluateOrLiteral(value, context);
        return ExpressionContext.ToFloat(result);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _luaEvaluator?.Dispose();
            _luaEvaluator = null;
            _cache.Clear();
            _isDisposed = true;
        }
    }
}
