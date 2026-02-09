using System;
using System.Threading.Tasks;
using engine;
using engine.narration;
using engine.quest;
using static engine.Logger;

namespace nogame.modules.story;


/// <summary>
/// Registers game-specific functions and event handlers with the NarrationManager.
/// Called during module activation to wire up quest triggers, inventory lookups, etc.
/// </summary>
public static class NarrationBindings
{
    private static void _registerQuestFactories(QuestFactory questFactory)
    {
        questFactory.RegisterQuest("nogame.quests.VisitAgentTwelve.Quest",
            async (engine, eQuest) =>
            {
                var targetPos =
                    await nogame.quests.VisitAgentTwelve.VisitAgentTwelveStrategy
                        .ComputeTargetLocationAsync(engine);

                await engine.TaskMainThread(() =>
                {
                    eQuest.Set(new engine.quest.components.QuestInfo
                    {
                        QuestId = "nogame.quests.VisitAgentTwelve.Quest",
                        Title = "Come to the location.",
                        ShortDescription = "Try to find the marker on the map and reach it.",
                        LongDescription =
                            "Every journey starts with the first step. Reach for the third step" +
                            " to make it an experience."
                    });

                    eQuest.Set(new engine.behave.components.Strategy(
                        new nogame.quests.VisitAgentTwelve.VisitAgentTwelveStrategy(targetPos)));
                });
            });
    }


    public static void Register(NarrationManager manager)
    {
        var questFactory = I.Get<QuestFactory>();
        _registerQuestFactories(questFactory);

        // Quest triggering: used in narration event descriptors like
        // { "type": "quest.trigger", "quest": "nogame.quests.VisitAgentTwelve.Quest" }
        // New-style quests use QuestFactory; unregistered quests fall back to old Manager.
        manager.RegisterEventHandler("quest.trigger", async (desc) =>
        {
            if (desc.Params.TryGetValue("quest", out var questNameObj))
            {
                string questName = questNameObj.ToString();
                try
                {
                    if (questFactory.HasQuest(questName))
                    {
                        await questFactory.TriggerQuest(questName, true);
                    }
                    else
                    {
                        await I.Get<engine.quest.Manager>().TriggerQuest(questName, true);
                    }
                }
                catch (Exception e)
                {
                    Warning($"NarrationBindings: quest.trigger failed for '{questName}': {e.Message}");
                }
            }
        });

        // Property set: used in narration events like
        // { "type": "props.set", "key": "someProp", "value": "someValue" }
        manager.RegisterEventHandler("props.set", async (desc) =>
        {
            if (desc.Params.TryGetValue("key", out var keyObj) && desc.Params.TryGetValue("value", out var valueObj))
            {
                Props.Set(keyObj.ToString(), valueObj);
            }

            await Task.CompletedTask;
        });

        // Register interpolation functions available in narration text as {func.propValue(key)}
        manager.RegisterFunction("propValue", (args) =>
        {
            if (args.Length > 0)
            {
                var val = Props.Get(args[0], "");
                return val?.ToString() ?? "";
            }

            return "";
        });
    }
}
