using System.Text.Json.Serialization;

namespace engine.quest.components;

/// <summary>
/// Pure-data component for quest identity and state.
/// Replaces IQuest interface — behavior comes from the Strategy component.
/// </summary>
[engine.IsPersistable]
public struct QuestInfo
{
    [JsonInclude] public string QuestId;
    [JsonInclude] public string Title;
    [JsonInclude] public string ShortDescription;
    [JsonInclude] public string LongDescription;
    [JsonInclude] public bool IsActive;
    [JsonInclude] public byte State;
    [JsonInclude] public float Progress;
}
