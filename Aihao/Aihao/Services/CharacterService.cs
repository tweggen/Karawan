using System.Collections.Generic;
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
