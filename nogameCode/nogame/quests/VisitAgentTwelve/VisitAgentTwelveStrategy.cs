using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.joyce.components;
using engine.world.components;
using static engine.Logger;

namespace nogame.quests.VisitAgentTwelve;

/// <summary>
/// Top-level strategy for the VisitAgentTwelve quest.
/// Single phase: navigate to the nearest bar.
/// </summary>
public class VisitAgentTwelveStrategy : AOneOfStrategy
{
    public override string GetStartStrategy() => "navigate";


    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["navigate"])
        {
            // Navigation complete — exit the state machine, then trigger narration and cleanup.
            TriggerStrategy(null);
            I.Get<nogame.modules.story.Narration>().TriggerNarration("agent12", "");
            I.Get<engine.quest.QuestFactory>().DeactivateQuest(_entity);
        }
    }


    /// <summary>
    /// Find the nearest "drink" map icon to the player.
    /// Must be called with access to the main thread.
    /// </summary>
    public static async Task<Vector3> ComputeTargetLocationAsync(Engine engine)
    {
        Vector3 v3Pos = default;

        await engine.TaskMainThread(() =>
        {
            var withIcon = engine.GetEcsWorld().GetEntities()
                .With<Transform3ToWorld>().With<MapIcon>().AsEnumerable();
            float mind2 = Single.MaxValue;
            Entity eClosest = default;

            if (engine.Player.TryGet(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
            {
                var v3Player = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
                foreach (var e in withIcon)
                {
                    ref var cMapIcon = ref e.Get<MapIcon>();
                    if (cMapIcon.Code != MapIcon.IconCode.Drink) continue;
                    var v3D = v3Player - e.Get<Transform3ToWorld>().Matrix.Translation;
                    var d2d2 = new Vector2(v3D.X, v3D.Z).LengthSquared();
                    if (d2d2 > 20f && d2d2 < mind2)
                    {
                        if (e.IsAlive && e.IsEnabled())
                        {
                            eClosest = e;
                            mind2 = d2d2;
                        }
                    }
                }
            }

            if (default == eClosest)
            {
                Error("Unable to find any marker close to player entity.");
                v3Pos = Vector3.Zero;
            }
            else
            {
                v3Pos = eClosest.Get<Transform3ToWorld>().Matrix.Translation;
            }
        });

        return v3Pos;
    }


    public VisitAgentTwelveStrategy(Vector3 destinationPosition)
    {
        Strategies = new()
        {
            {
                "navigate", new NavigateStrategy()
                {
                    Controller = this,
                    DestinationPosition = destinationPosition
                }
            }
        };
    }
}
