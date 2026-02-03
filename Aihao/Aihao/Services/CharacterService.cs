using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service that provides access to character data from the project configuration.
/// Used by other editors (like NarrationEditor) to get character IDs for autocomplete.
/// </summary>
public class CharacterService
{
    private readonly AihaoProject _project;

    public CharacterService(AihaoProject project)
    {
        _project = project;
    }

    /// <summary>
    /// Extract all speaker IDs from a narration JSON object.
    /// Scans all scripts and nodes for speaker statements.
    /// </summary>
    public static IEnumerable<string> ExtractSpeakersFromNarration(JsonObject narrationObj)
    {
        var speakers = new HashSet<string>();

        if (!narrationObj.TryGetPropertyValue("scripts", out var scriptsNode) ||
            scriptsNode is not JsonObject scriptsObj)
        {
            return speakers;
        }

        foreach (var scriptKvp in scriptsObj)
        {
            if (scriptKvp.Value is not JsonObject scriptObj)
                continue;

            if (!scriptObj.TryGetPropertyValue("nodes", out var nodesNode) ||
                nodesNode is not JsonObject nodesObj)
                continue;

            foreach (var nodeKvp in nodesObj)
            {
                if (nodeKvp.Value is not JsonObject nodeObj)
                    continue;

                if (!nodeObj.TryGetPropertyValue("flow", out var flowNode) ||
                    flowNode is not JsonArray flowArr)
                    continue;

                foreach (var stmt in flowArr)
                {
                    if (stmt is not JsonObject stmtObj)
                        continue;

                    if (stmtObj.TryGetPropertyValue("speaker", out var speakerNode) &&
                        speakerNode is JsonValue speakerVal &&
                        speakerVal.TryGetValue<string>(out var speaker) &&
                        !string.IsNullOrWhiteSpace(speaker))
                    {
                        speakers.Add(speaker);
                    }
                }
            }
        }

        return speakers;
    }

    /// <summary>
    /// Add missing characters from a list of speaker IDs.
    /// Returns the number of characters added.
    /// </summary>
    public int AddMissingCharacters(IEnumerable<string> speakerIds)
    {
        var content = _project.GetSection("characters");
        if (content is not JsonObject charactersObj)
            return 0;

        var existingIds = new HashSet<string>(
            charactersObj.Where(kvp => !kvp.Key.StartsWith("__")).Select(kvp => kvp.Key),
            StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var speakerId in speakerIds)
        {
            if (string.IsNullOrWhiteSpace(speakerId))
                continue;

            if (existingIds.Contains(speakerId))
                continue;

            // Create a new character entry with auto-generated display name
            var displayName = GenerateDisplayName(speakerId);
            var newCharacter = new JsonObject
            {
                ["displayName"] = displayName,
                ["description"] = $"Character discovered from narration",
                ["portrait"] = null,
                ["properties"] = new JsonObject()
            };

            charactersObj[speakerId] = newCharacter;
            existingIds.Add(speakerId);
            added++;
        }

        return added;
    }

    /// <summary>
    /// Generate a human-readable display name from a character ID.
    /// Converts camelCase/snake_case to Title Case.
    /// </summary>
    private static string GenerateDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;

        // Handle snake_case
        var withSpaces = id.Replace('_', ' ').Replace('-', ' ');

        // Handle camelCase - insert space before uppercase letters
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < withSpaces.Length; i++)
        {
            var c = withSpaces[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(withSpaces[i - 1]))
            {
                result.Append(' ');
            }
            result.Append(c);
        }

        // Convert to title case
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(result.ToString().ToLower());
    }

    /// <summary>
    /// Get all character IDs from the /characters section.
    /// </summary>
    public IEnumerable<string> GetCharacterIds()
    {
        var content = _project.GetSection("characters");
        if (content is not JsonObject charactersObj)
            return Enumerable.Empty<string>();

        return charactersObj
            .Where(kvp => !kvp.Key.StartsWith("__"))
            .Select(kvp => kvp.Key)
            .OrderBy(id => id);
    }

    /// <summary>
    /// Get a character's display name by ID.
    /// </summary>
    public string? GetDisplayName(string characterId)
    {
        var content = _project.GetSection("characters");
        if (content is not JsonObject charactersObj)
            return null;

        if (!charactersObj.TryGetPropertyValue(characterId, out var charNode) ||
            charNode is not JsonObject charObj)
            return null;

        if (charObj.TryGetPropertyValue("displayName", out var displayNameNode) &&
            displayNameNode is JsonValue displayNameVal &&
            displayNameVal.TryGetValue<string>(out var displayName))
        {
            return displayName;
        }

        return null;
    }

    /// <summary>
    /// Get all characters as ID -> DisplayName pairs.
    /// </summary>
    public IEnumerable<(string Id, string DisplayName)> GetCharacters()
    {
        var content = _project.GetSection("characters");
        if (content is not JsonObject charactersObj)
            yield break;

        foreach (var kvp in charactersObj.OrderBy(x => x.Key))
        {
            if (kvp.Key.StartsWith("__"))
                continue;

            var displayName = kvp.Key;
            if (kvp.Value is JsonObject charObj &&
                charObj.TryGetPropertyValue("displayName", out var displayNameNode) &&
                displayNameNode is JsonValue displayNameVal &&
                displayNameVal.TryGetValue<string>(out var dn))
            {
                displayName = dn;
            }

            yield return (kvp.Key, displayName);
        }
    }
}
