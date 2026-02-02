using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.narration;


/// <summary>
/// Resolves interpolation tokens in narration text strings.
///
/// Token formats:
///   {props.keyName}            — lookup from Props
///   {func.name(arg1, arg2)}    — call registered sync or async function
///   {bindingName}              — lookup from named bindings (shorthand aliases)
///
/// Unresolved tokens render as [?tokenName?] for debuggability.
/// </summary>
public class NarrationInterpolator
{
    private readonly Dictionary<string, Func<string[], string>> _syncFunctions = new();
    private readonly Dictionary<string, Func<string[], Task<string>>> _asyncFunctions = new();
    private readonly Dictionary<string, string> _bindings = new();


    public void RegisterFunction(string name, Func<string[], string> fn)
    {
        _syncFunctions[name] = fn;
    }


    public void RegisterAsyncFunction(string name, Func<string[], Task<string>> fn)
    {
        _asyncFunctions[name] = fn;
    }


    /// <summary>
    /// Register a named binding alias. E.g. binding "playerName" => "props.playerName"
    /// means {playerName} in text resolves the same as {props.playerName}.
    /// </summary>
    public void RegisterBinding(string name, string expression)
    {
        _bindings[name] = expression;
    }


    /// <summary>
    /// Interpolate all tokens in the template string.
    /// </summary>
    public async Task<string> Interpolate(string template)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains('{'))
        {
            return template;
        }

        var sb = new StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                int end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    sb.Append(template[i]);
                    i++;
                    continue;
                }

                string token = template.Substring(i + 1, end - i - 1).Trim();
                string resolved = await _resolveToken(token);
                sb.Append(resolved);
                i = end + 1;
            }
            else
            {
                sb.Append(template[i]);
                i++;
            }
        }

        return sb.ToString();
    }


    /// <summary>
    /// Synchronous interpolation for cases where async is not needed.
    /// Will block on any async functions.
    /// </summary>
    public string InterpolateSync(string template)
    {
        return Interpolate(template).GetAwaiter().GetResult();
    }


    private async Task<string> _resolveToken(string token)
    {
        // Check named bindings first
        if (_bindings.TryGetValue(token, out string boundExpr))
        {
            return await _resolveToken(boundExpr);
        }

        // props.keyName
        if (token.StartsWith("props."))
        {
            string key = token.Substring(6);
            var value = Props.Get(key, null);
            if (value != null)
            {
                return value.ToString();
            }

            return $"[?{token}?]";
        }

        // func.name(args)
        if (token.StartsWith("func."))
        {
            string funcExpr = token.Substring(5);
            int parenIdx = funcExpr.IndexOf('(');
            string funcName;
            string[] args;
            if (parenIdx >= 0)
            {
                funcName = funcExpr.Substring(0, parenIdx);
                string argsStr = funcExpr.Substring(parenIdx + 1).TrimEnd(')');
                args = string.IsNullOrWhiteSpace(argsStr)
                    ? Array.Empty<string>()
                    : argsStr.Split(',', StringSplitOptions.TrimEntries);
            }
            else
            {
                funcName = funcExpr;
                args = Array.Empty<string>();
            }

            // Try async first, then sync
            if (_asyncFunctions.TryGetValue(funcName, out var asyncFn))
            {
                try
                {
                    return await asyncFn(args);
                }
                catch (Exception e)
                {
                    Warning($"NarrationInterpolator: async func '{funcName}' threw: {e.Message}");
                    return $"[?{token}?]";
                }
            }

            if (_syncFunctions.TryGetValue(funcName, out var syncFn))
            {
                try
                {
                    return syncFn(args);
                }
                catch (Exception e)
                {
                    Warning($"NarrationInterpolator: func '{funcName}' threw: {e.Message}");
                    return $"[?{token}?]";
                }
            }

            Warning($"NarrationInterpolator: unknown function '{funcName}'");
            return $"[?{token}?]";
        }

        return $"[?{token}?]";
    }
}
