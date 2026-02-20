using System.Collections.Generic;
using engine;
using engine.quest.components;

namespace nogame.quests;

/// <summary>
/// Lua bindings for the quest log UI.
/// </summary>
public class QuestLuaBindings
{
    public List<SortedDictionary<string, object>> getQuestList()
    {
        var listOfQuests = new List<SortedDictionary<string, object>>();
        foreach (var eQuest in
                 I.Get<Engine>().GetEcsWorld().GetEntities()
                     .With<QuestInfo>()
                     .AsEnumerable())
        {
            ref var cQuestInfo = ref eQuest.Get<QuestInfo>();
            var dictQuest = new SortedDictionary<string, object>();
            dictQuest.Add("id", cQuestInfo.QuestId ?? "");
            dictQuest.Add("title", cQuestInfo.Title ?? "Untitled Quest");
            dictQuest.Add("description", cQuestInfo.ShortDescription ?? "");
            dictQuest.Add("active", cQuestInfo.IsActive);
            listOfQuests.Add(dictQuest);
        }

        return listOfQuests;
    }
}
