using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.quest;
using engine.quest.components;
using engine.world;
using static engine.Logger;

namespace nogame.quests.HelloFishmonger;


/**
 * Saves a running HelloFishmonger chase and resumes it on load (KAR-475), as
 * TaxiQuestCreator does for a taxi run.
 *
 * Neither the quest entity's Strategy nor the car survive a save by themselves. The quest
 * entity carries this creator and its persistable QuestInfo; the creator's own JSON holds
 * where the car drives (HelloFishmongerSaveState). On load the car is built again at the
 * junction it was driving away from, and the trail strategy is attached to the quest.
 *
 * Building the car needs the fragment under that junction to be loaded, which right after
 * a load it may not yet be, and EntitySaver waits for every creator. So the load returns
 * at once and the car is made once the fragment is there.
 */
public class HelloFishmongerQuestCreator : ICreator
{
    public const string QuestId = "nogame.quests.HelloFishmonger.Quest";

    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _giveUpAfter = TimeSpan.FromMinutes(5);


    public Task<Action> SetupEntityFrom(Entity eLoaded, JsonElement je)
    {
        var state = HelloFishmongerSaveState.FromJson(je);
        return Task.FromResult<Action>(() =>
        {
            if (state == null)
            {
                Warning($"HelloFishmongerQuestCreator: saved chase holds no car position; ending the quest.");
                I.Get<QuestFactory>().DeactivateQuest(eLoaded);
                return;
            }

            _ = _resumeAsync(eLoaded, state.Value);
        });
    }


    /*
     * Fire and forget, so it catches everything (an escaping fault would kill the process).
     */
    private static async Task _resumeAsync(Entity eQuest, HelloFishmongerSaveState state)
    {
        try
        {
            var engine = I.Get<Engine>();
            var clusterList = I.Get<ClusterList>().GetClusterList();
            if (state.ClusterIndex < 0 || state.ClusterIndex >= clusterList.Count)
            {
                Warning($"HelloFishmongerQuestCreator: saved cluster {state.ClusterIndex} does not exist.");
                await engine.TaskMainThread(() => _endQuest(eQuest));
                return;
            }

            var clusterDesc = clusterList[state.ClusterIndex];

            // Street generation runs here, off the logical thread.
            var spStart = FishmongerCar.NearestStreetPoint(clusterDesc, state.DrivingFrom);
            if (spStart == null)
            {
                Warning($"HelloFishmongerQuestCreator: cluster {clusterDesc.Name} has no street points.");
                await engine.TaskMainThread(() => _endQuest(eQuest));
                return;
            }

            var mcp = FishmongerCar.ModelParams();
            var model = await FishmongerCar.LoadModelAsync(mcp);

            var idxFragment = Fragment.PosToIndex3(spStart.Pos3 + clusterDesc.Pos);
            var waited = TimeSpan.Zero;
            while (true)
            {
                bool done = false, gone = false;
                await engine.TaskMainThread(() =>
                {
                    if (!eQuest.IsAlive)
                    {
                        gone = true;
                        return;
                    }
                    if (!I.Get<MetaGen>().Loader.TryGetFragment(idxFragment, out var worldFragment)
                        || worldFragment == null)
                    {
                        return;
                    }

                    var eCar = FishmongerCar.CreateLT(engine, model, mcp, clusterDesc, worldFragment, spStart);
                    eQuest.Set(new engine.behave.components.Strategy(new HelloFishmongerStrategy(eCar)));
                    I.Get<engine.news.EventQueue>().Push(new QuestTriggeredEvent(QuestId));
                    Trace($"HelloFishmongerQuestCreator: chase resumed from {spStart.Pos} in {clusterDesc.Name}.");
                    done = true;
                });

                if (done || gone) return;

                if (waited >= _giveUpAfter)
                {
                    Warning($"HelloFishmongerQuestCreator: fragment {idxFragment} never loaded; the chase is not resumed.");
                    return;
                }
                await Task.Delay(_pollInterval);
                waited += _pollInterval;
            }
        }
        catch (Exception e)
        {
            Error($"HelloFishmongerQuestCreator: resuming the chase failed: {e}");
        }
    }


    private static void _endQuest(Entity eQuest)
    {
        if (eQuest.IsAlive) I.Get<QuestFactory>().DeactivateQuest(eQuest);
    }


    /**
     * The cluster and the junction the car drives away from. Nothing when there is no car
     * on a street, in which case the load ends the quest rather than resuming it.
     * Logical thread (EntitySaver.SaveAll).
     */
    public void SaveEntityTo(Entity eQuest, out JsonNode jn)
    {
        jn = new JsonObject();
        if (!eQuest.Has<engine.behave.components.Strategy>()) return;
        if (eQuest.Get<engine.behave.components.Strategy>().EntityStrategy is not HelloFishmongerStrategy strategy)
        {
            return;
        }

        if (FishmongerCar.DrivingFrom(strategy.CarEntity) is not { } driving) return;

        jn = new HelloFishmongerSaveState(driving.Cluster.Index, driving.From.Pos).ToJson();
    }
}
