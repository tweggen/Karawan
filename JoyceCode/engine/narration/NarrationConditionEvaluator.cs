using System;
using System.Collections.Generic;
using System.Globalization;
using static engine.Logger;

namespace engine.narration;


/// <summary>
/// Evaluates condition expressions used in narration goto and node/choice conditions.
///
/// Supported syntax:
///   props.keyName              — truthy check
///   props.keyName > 10         — comparison: >, <, >=, <=, ==, !=
///   func.name(arg1, arg2)      — call registered function, truthy check
///   !expr                      — negation
///   expr && expr               — logical AND
///   expr || expr               — logical OR
/// </summary>
public class NarrationConditionEvaluator
{
    private readonly Dictionary<string, Func<string[], string>> _functions = new();


    public void RegisterFunction(string name, Func<string[], string> fn)
    {
        _functions[name] = fn;
    }


    /// <summary>
    /// Evaluate a condition string. Returns true if the condition is met.
    /// </summary>
    public bool Evaluate(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        try
        {
            var tokens = _tokenize(condition);
            int pos = 0;
            return _parseOr(tokens, ref pos);
        }
        catch (Exception e)
        {
            Warning($"NarrationConditionEvaluator: error evaluating '{condition}': {e.Message}");
            return false;
        }
    }


    private bool _parseOr(List<string> tokens, ref int pos)
    {
        bool result = _parseAnd(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "||")
        {
            pos++;
            result = _parseAnd(tokens, ref pos) || result;
        }

        return result;
    }


    private bool _parseAnd(List<string> tokens, ref int pos)
    {
        bool result = _parseUnary(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "&&")
        {
            pos++;
            result = _parseUnary(tokens, ref pos) && result;
        }

        return result;
    }


    private bool _parseUnary(List<string> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos] == "!")
        {
            pos++;
            return !_parseUnary(tokens, ref pos);
        }

        return _parseComparison(tokens, ref pos);
    }


    private bool _parseComparison(List<string> tokens, ref int pos)
    {
        var left = _resolveValue(tokens, ref pos);

        if (pos < tokens.Count && _isComparisonOp(tokens[pos]))
        {
            string op = tokens[pos];
            pos++;
            var right = _resolveValue(tokens, ref pos);
            return _compare(left, op, right);
        }

        return _isTruthy(left);
    }


    private object _resolveValue(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
        {
            return null;
        }

        string token = tokens[pos];

        // props.keyName
        if (token.StartsWith("props."))
        {
            pos++;
            string key = token.Substring(6);
            return Props.Get(key, null);
        }

        // func.name(args)
        if (token.StartsWith("func."))
        {
            pos++;
            string funcExpr = token.Substring(5);
            int parenIdx = funcExpr.IndexOf('(');
            if (parenIdx >= 0)
            {
                string funcName = funcExpr.Substring(0, parenIdx);
                string argsStr = funcExpr.Substring(parenIdx + 1).TrimEnd(')');
                string[] args = string.IsNullOrWhiteSpace(argsStr)
                    ? Array.Empty<string>()
                    : argsStr.Split(',', StringSplitOptions.TrimEntries);
                if (_functions.TryGetValue(funcName, out var fn))
                {
                    return fn(args);
                }

                Warning($"NarrationConditionEvaluator: unknown function '{funcName}'");
                return null;
            }
            else
            {
                // func.name without parens — treat as no-arg call
                if (_functions.TryGetValue(funcExpr, out var fn))
                {
                    return fn(Array.Empty<string>());
                }

                Warning($"NarrationConditionEvaluator: unknown function '{funcExpr}'");
                return null;
            }
        }

        // Numeric literal
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
        {
            pos++;
            return num;
        }

        // String literal (quoted)
        if (token.StartsWith("'") && token.EndsWith("'") && token.Length >= 2)
        {
            pos++;
            return token.Substring(1, token.Length - 2);
        }

        // Boolean literals
        if (token == "true")
        {
            pos++;
            return true;
        }

        if (token == "false")
        {
            pos++;
            return false;
        }

        // Unknown token — return as string
        pos++;
        return token;
    }


    private static bool _isComparisonOp(string token)
    {
        return token is ">" or "<" or ">=" or "<=" or "==" or "!=";
    }


    private static bool _compare(object left, string op, object right)
    {
        double? leftNum = _toDouble(left);
        double? rightNum = _toDouble(right);

        if (leftNum.HasValue && rightNum.HasValue)
        {
            return op switch
            {
                ">" => leftNum.Value > rightNum.Value,
                "<" => leftNum.Value < rightNum.Value,
                ">=" => leftNum.Value >= rightNum.Value,
                "<=" => leftNum.Value <= rightNum.Value,
                "==" => Math.Abs(leftNum.Value - rightNum.Value) < 0.0001,
                "!=" => Math.Abs(leftNum.Value - rightNum.Value) >= 0.0001,
                _ => false
            };
        }

        string leftStr = left?.ToString() ?? "";
        string rightStr = right?.ToString() ?? "";

        return op switch
        {
            "==" => leftStr == rightStr,
            "!=" => leftStr != rightStr,
            _ => false
        };
    }


    private static double? _toDouble(object value)
    {
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return null;
    }


    private static bool _isTruthy(object value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s) && s != "false" && s != "0";
        if (value is double d) return d != 0.0;
        if (value is float f) return f != 0f;
        if (value is int i) return i != 0;
        return true;
    }


    /// <summary>
    /// Simple tokenizer: splits on whitespace, keeping operators and func() calls as single tokens.
    /// </summary>
    private static List<string> _tokenize(string input)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < input.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            // Two-char operators
            if (i + 1 < input.Length)
            {
                string twoChar = input.Substring(i, 2);
                if (twoChar is "&&" or "||" or ">=" or "<=" or "==" or "!=")
                {
                    tokens.Add(twoChar);
                    i += 2;
                    continue;
                }
            }

            // Single-char operators
            if (input[i] is '>' or '<' or '!')
            {
                tokens.Add(input[i].ToString());
                i++;
                continue;
            }

            // Quoted string
            if (input[i] == '\'')
            {
                int end = input.IndexOf('\'', i + 1);
                if (end < 0) end = input.Length;
                tokens.Add(input.Substring(i, end - i + 1));
                i = end + 1;
                continue;
            }

            // Word token (may include dots, parens for func calls)
            int start = i;
            int parenDepth = 0;
            while (i < input.Length)
            {
                if (input[i] == '(')
                {
                    parenDepth++;
                    i++;
                }
                else if (input[i] == ')' && parenDepth > 0)
                {
                    parenDepth--;
                    i++;
                    if (parenDepth == 0) break;
                }
                else if (parenDepth > 0)
                {
                    i++;
                }
                else if (char.IsWhiteSpace(input[i]) || input[i] is '&' or '|' or '>' or '<' or '=' or '!')
                {
                    break;
                }
                else
                {
                    i++;
                }
            }

            if (i > start)
            {
                tokens.Add(input.Substring(start, i - start));
            }
        }

        return tokens;
    }
}
