using System;
using System.Threading.Tasks;
using engine;
using engine.narration;
using static engine.Logger;

namespace nogame.modules.story;


/// <summary>
/// Registers game-specific functions and event handlers with the NarrationManager.
/// Called during module activation to wire up quest triggers, inventory lookups, etc.
/// </summary>
public static class NarrationBindings
{
    public static void Register(NarrationManager manager)
    {
        // Quest triggering: used in narration event descriptors like
        // { "type": "quest.trigger", "quest": "nogame.quests.VisitAgentTwelve.Quest" }
        manager.RegisterEventHandler("quest.trigger", async (desc) =>
        {
            if (desc.Params.TryGetValue("quest", out var questNameObj))
            {
                string questName = questNameObj.ToString();
                try
                {
                    I.Get<engine.quest.Manager>().TriggerQuest(questName, true);
                }
                catch (Exception e)
                {
                    Warning($"NarrationBindings: quest.trigger failed for '{questName}': {e.Message}");
                }
            }

            await Task.CompletedTask;
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
