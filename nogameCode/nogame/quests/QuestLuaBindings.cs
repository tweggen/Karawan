using System.Collections.Generic;
using engine;
using engine.quest;
using engine.quest.components;

namespace nogame.quests;

/// <summary>
/// Lua bindings for the quest log UI.
/// </summary>
public class QuestLuaBindings
{
    public List<SortedDictionary<string, object>> getQuestList()
    {
        var satnavService = I.TryGet<ISatnavService>();
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
            dictQuest.Add("followed", satnavService != null && satnavService.IsFollowed(eQuest));
            listOfQuests.Add(dictQuest);
        }

        return listOfQuests;
    }


    public void followQuest(string questId)
    {
        I.Get<ISatnavService>().FollowQuest(questId);
    }


    public void unfollowQuest()
    {
        I.Get<ISatnavService>().UnfollowQuest();
    }


    public bool isFollowed(string questId)
    {
        var svc = I.TryGet<ISatnavService>();
        if (svc == null) return false;

        foreach (var eQuest in
                 I.Get<Engine>().GetEcsWorld().GetEntities()
                     .With<QuestInfo>()
                     .AsEnumerable())
        {
            if (eQuest.Get<QuestInfo>().QuestId == questId)
            {
                return svc.IsFollowed(eQuest);
            }
        }

        return false;
    }
}
