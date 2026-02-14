using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.quest;
using engine.world;
using static engine.Logger;

namespace nogame.quests.Taxi;

/// <summary>
/// ICreator implementation for taxi quest entities.
/// Reconstructs the TaxiQuestStrategy from persisted TaxiQuestData on load,
/// and updates TaxiQuestData.Phase from the active strategy on save.
/// </summary>
public class TaxiQuestCreator : ICreator
{
    private const string _questId = "nogame.quests.Taxi.Quest";

    public Func<Task> SetupEntityFrom(Entity eLoaded, JsonElement je)
    {
        return () =>
        {
            if (!eLoaded.Has<TaxiQuestData>())
            {
                Warning($"TaxiQuestCreator: entity missing TaxiQuestData, cannot restore quest.");
                return Task.CompletedTask;
            }
            var data = eLoaded.Get<TaxiQuestData>();

            string startPhase = data.Phase switch
            {
                0 => "pickup",
                1 => "driving",
                _ => "pickup"
            };

            var strategy = new TaxiQuestStrategy(data.GuestPosition, data.DestinationPosition, startPhase);
            eLoaded.Set(new engine.behave.components.Strategy(strategy));

            I.Get<engine.news.EventQueue>().Push(new QuestTriggeredEvent(_questId));

            return Task.CompletedTask;
        };
    }


    public void SaveEntityTo(Entity eLoader, out JsonNode jn)
    {
        jn = new JsonObject();

        if (eLoader.Has<TaxiQuestData>() && eLoader.Has<engine.behave.components.Strategy>())
        {
            ref var data = ref eLoader.Get<TaxiQuestData>();
            ref var stratComp = ref eLoader.Get<engine.behave.components.Strategy>();

            if (stratComp.EntityStrategy is TaxiQuestStrategy taxiStrategy)
            {
                var active = taxiStrategy.GetActiveStrategy();
                if (active is DrivingStrategy)
                {
                    data.Phase = 1;
                }
                else
                {
                    data.Phase = 0;
                }
            }
        }
    }
}
