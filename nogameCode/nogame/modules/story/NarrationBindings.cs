using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.narration;
using engine.physics;
using engine.quest;
using engine.streets;
using engine.world;
using engine.world.components;
using nogame.characters.car3;
using nogame.cities;
using static engine.Logger;
using Behavior = engine.behave.components.Behavior;

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

        questFactory.RegisterQuest("nogame.quests.HelloFishmonger.Quest",
            async (engine, eQuest) =>
            {
                string targetCarName = "Fishmonger's car";

                var mcp = new ModelCacheParams()
                {
                    Url = "car6.obj",
                    Properties = new()
                    {
                        ["primarycolor"] = "#ffffff00"
                    },
                    Params = new()
                    {
                        GeomFlags = 0 | InstantiateModelParams.CENTER_X
                                      | InstantiateModelParams.CENTER_Z
                                      | InstantiateModelParams.ROTATE_Y180
                                      | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                                      | InstantiateModelParams.BUILD_PHYSICS
                                      | InstantiateModelParams.PHYSICS_DETECTABLE
                                      | InstantiateModelParams.PHYSICS_TANGIBLE
                                      | InstantiateModelParams.PHYSICS_CALLBACKS,
                        MaxDistance = 150,
                        SolidLayerMask = CollisionProperties.Layers.NpcVehicle,
                        SensitiveLayerMask = CollisionProperties.Layers.AnyVehicle,
                        Name = targetCarName
                    }
                };

                var model = await I.Get<ModelCache>().LoadModel(mcp);

                Entity eCarEntity = default;

                await engine.TaskMainThread(() =>
                {
                    float minDist = 100f;
                    float minDist2 = minDist * minDist;

                    var clusterDesc = I.Get<ClusterList>().GetClusterAt(Vector3.Zero);
                    Vector3 v3Cluster = clusterDesc.Pos;
                    var listStreetPoints = clusterDesc.StrokeStore().GetStreetPoints();
                    engine.Player.TryGet(out var ePlayer);

                    StreetPoint? spStart = listStreetPoints.FirstOrDefault(
                        sp =>
                            (sp.Pos3 with { Y = 0f } - ePlayer.Get<Transform3ToWorld>().Matrix.Translation with { Y = 0 })
                            .LengthSquared() >= minDist2
                            && I.Get<MetaGen>().Loader.TryGetFragment(
                                Fragment.PosToIndex3(sp.Pos3 + v3Cluster), out _));
                    if (null == spStart)
                    {
                        spStart = listStreetPoints.First();
                    }

                    I.Get<MetaGen>().Loader.TryGetFragment(
                        Fragment.PosToIndex3(spStart.Pos3 + v3Cluster), out var worldFragment);

                    eCarEntity = engine.CreateEntity(targetCarName);

                    CharacterCreator.SetupCharacterMT(
                        eCarEntity,
                        clusterDesc, worldFragment, spStart,
                        model, mcp,
                        new nogame.characters.car3.Behavior()
                        {
                            Navigator = new StreetNavigationController()
                            {
                                ClusterDesc = clusterDesc,
                                StartPoint = spStart,
                                Seed = 100,
                                Speed = 35f
                            }
                        },
                        null
                    );

                    eCarEntity.Set(new EntityName(targetCarName));
                    eCarEntity.Set(new Creator(Creator.CreatorId_Hardcoded));

                    ref var cBehavior = ref eCarEntity.Get<Behavior>();
                    cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;

                    eQuest.Set(new engine.quest.components.QuestInfo
                    {
                        QuestId = "nogame.quests.HelloFishmonger.Quest",
                        Title = "Trail the car.",
                        ShortDescription =
                            "The car quickly is departing. Follow it to its target!",
                        LongDescription = "Isn't this a chase again?"
                    });

                    eQuest.Set(new engine.behave.components.Strategy(
                        new nogame.quests.HelloFishmonger.HelloFishmongerStrategy(eCarEntity)));
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
