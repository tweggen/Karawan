#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace nogame.modules;


/**
 * Turns a failed game-server call into one short line the login screen
 * can show. The server's own message is preferred whenever the body
 * carries one; the shape of that body is not pinned, so common JSON
 * error shapes and short plain-text bodies are all accepted.
 *
 * Depends on System.* only, so the test project can compile it directly.
 */
public static class ServerErrorText
{
    public const int MaxLength = 64;


    private static readonly string[] _errorKeys = { "error", "message", "errors", "detail", "reason" };


    public static string Describe(HttpStatusCode statusCode, string? reasonPhrase, string? body)
    {
        string? message = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var jdoc = JsonDocument.Parse(body);
                message = _findErrorText(jdoc.RootElement);
            }
            catch (JsonException)
            {
                string trimmed = body.Trim();
                if (trimmed.Length <= MaxLength && !trimmed.StartsWith("<"))
                {
                    message = trimmed;
                }
            }
        }

        int code = (int)statusCode;
        if (string.IsNullOrWhiteSpace(message))
        {
            return _clip($"server answered {code} {reasonPhrase}".TrimEnd());
        }

        return _clip($"{message.Trim()} ({code})");
    }


    public static string Describe(Exception e)
    {
        switch (e)
        {
            case OperationCanceledException:
                return "the server did not answer in time";
            case HttpRequestException:
                return _clip($"server not reachable: {e.Message}");
            case AggregateException { InnerException: not null } ae:
                return Describe(ae.InnerException!);
            default:
                return _clip(e.Message);
        }
    }


    private static string? _findErrorText(JsonElement je)
    {
        switch (je.ValueKind)
        {
            case JsonValueKind.String:
                return je.GetString();

            case JsonValueKind.Array:
                foreach (var jeItem in je.EnumerateArray())
                {
                    var text = _findErrorText(jeItem);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                return null;

            case JsonValueKind.Object:
                foreach (var key in _errorKeys)
                {
                    if (!je.TryGetProperty(key, out var jeChild)) continue;

                    /*
                     * A validation map such as {"errors": {"email": ["is invalid"]}}
                     * reads best as "email is invalid".
                     */
                    if (key == "errors" && jeChild.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in jeChild.EnumerateObject())
                        {
                            var fieldText = _findErrorText(prop.Value);
                            if (string.IsNullOrWhiteSpace(fieldText)) continue;

                            /*
                             * Phoenix's default {"errors": {"detail": "Not Found"}} is no field name.
                             */
                            return Array.IndexOf(_errorKeys, prop.Name) >= 0 ? fieldText : $"{prop.Name} {fieldText}";
                        }
                        continue;
                    }

                    var text = _findErrorText(jeChild);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                return null;

            default:
                return null;
        }
    }


    private static string _clip(string text)
    {
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        return text.Length <= MaxLength ? text : text.Substring(0, MaxLength - 3) + "...";
    }
}
