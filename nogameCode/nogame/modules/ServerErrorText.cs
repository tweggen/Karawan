#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace nogame.modules;


/**
 * Turns a failed game-server call into one line the login screen can
 * show. The server's own message is preferred whenever the body carries
 * one: exli answers {"error": "..."} (KAR-423), older exli and proxies
 * answer plain text, and other common JSON error shapes are accepted too.
 *
 * The text is not clipped here, so the prefix a caller adds stays inside
 * the one limit: StatusLine clips to what fits the login screen.
 *
 * Depends on System.* only, so the test project can compile it directly.
 */
public static class ServerErrorText
{
    /**
     * Plain-text bodies longer than this are no message but a page.
     */
    public const int MaxPlainTextLength = 200;

    /**
     * What fits the login screen's 560 px status line in Prototype 17:
     * running text is about 8 px per character. The longest line exli
     * causes, "access refused: I'm sorry, I don't remember you. Please log
     * in again.", is 69 characters and 550 px (measured with SkiaSharp).
     */
    public const int StatusLineLength = 69;

    /**
     * exli's answer to GET /api/auth/save_game for an account without a
     * save; the current server answers 404 with plain text containing
     * _legacyNoSaveText instead.
     */
    public const string NoSaveYetError = "no saved game yet";
    private const string _legacyNoSaveText = "didn't save any game yet";


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
                if (trimmed.Length <= MaxPlainTextLength && !trimmed.StartsWith("<"))
                {
                    message = trimmed;
                }
            }
        }

        int code = (int)statusCode;
        if (string.IsNullOrWhiteSpace(message))
        {
            return _oneLine($"server answered {code} {reasonPhrase}".TrimEnd());
        }

        /*
         * A 2xx that still failed (the old exli rejected a login with 200)
         * must not read like success.
         */
        if (code >= 200 && code < 300)
        {
            return _oneLine(message.Trim());
        }

        return _oneLine($"{message.Trim()} ({code})");
    }


    /**
     * Why the server refused a login. 401 is exli's "wrong eMail or
     * password"; any other 4xx means the request itself was wrong, which
     * the player cannot fix by retyping credentials.
     */
    public static string DescribeLogin(HttpStatusCode statusCode, string? reasonPhrase, string? body)
    {
        string reason = Describe(statusCode, reasonPhrase, body);
        int code = (int)statusCode;
        if (code >= 400 && code < 500 && statusCode != HttpStatusCode.Unauthorized)
        {
            return $"server rejected the request: {reason}";
        }

        return $"login failed: {reason}";
    }


    /**
     * Whether a GET /api/auth/save_game answer is exli saying the account
     * has no save yet. Only exli's own 404 qualifies: a proxy's 404 while
     * exli redeploys ("404 page not found" from Traefik) must not start
     * the local game for a player who has an online save.
     */
    public static bool IsNoSaveYet(HttpStatusCode statusCode, string? body)
    {
        if (statusCode != HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var jdoc = JsonDocument.Parse(body);
            return jdoc.RootElement.ValueKind == JsonValueKind.Object
                   && jdoc.RootElement.TryGetProperty("error", out var jeError)
                   && jeError.ValueKind == JsonValueKind.String
                   && jeError.GetString() == NoSaveYetError;
        }
        catch (JsonException)
        {
            return body.Contains(_legacyNoSaveText, StringComparison.Ordinal);
        }
    }


    /**
     * Fit one status text to the login screen's status line.
     */
    public static string StatusLine(string? text)
    {
        string line = _oneLine(text ?? "");
        return line.Length <= StatusLineLength ? line : line.Substring(0, StatusLineLength - 3) + "...";
    }


    public static string Describe(Exception e)
    {
        switch (e)
        {
            case OperationCanceledException:
                return "the server did not answer in time";
            case HttpRequestException:
                return _oneLine($"server not reachable: {e.Message}");
            case AggregateException { InnerException: not null } ae:
                return Describe(ae.InnerException!);
            default:
                return _oneLine(e.Message);
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


    private static string _oneLine(string text) => text.Replace('\n', ' ').Replace('\r', ' ');
}
