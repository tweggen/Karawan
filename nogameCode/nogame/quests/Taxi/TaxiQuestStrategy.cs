using System.Numerics;
using engine;
using engine.behave;
using engine.behave.strategies;

namespace nogame.quests.Taxi;

/// <summary>
/// Top-level strategy for the Taxi quest.
/// Two phases: pickup guest -> drive to destination -> complete.
/// </summary>
public class TaxiQuestStrategy : AOneOfStrategy
{
    private readonly string _startPhase;

    public override string GetStartStrategy() => _startPhase;


    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["pickup"])
        {
            if (_entity.Has<TaxiQuestData>())
            {
                ref var data = ref _entity.Get<TaxiQuestData>();
                data.Phase = 1;
            }

            TriggerStrategy("driving");
        }
        else if (strategy == Strategies["driving"])
        {
            TriggerStrategy(null);
            I.Get<engine.quest.QuestFactory>().DeactivateQuest(_entity);
        }
    }


    public TaxiQuestStrategy(Vector3 guestPosition, Vector3 destinationPosition, string startPhase = "pickup")
    {
        _startPhase = startPhase;
        Strategies = new()
        {
            {
                "pickup", new PickupStrategy()
                {
                    Controller = this,
                    GuestPosition = guestPosition
                }
            },
            {
                "driving", new DrivingStrategy()
                {
                    Controller = this,
                    DestinationPosition = destinationPosition
                }
            }
        };
    }
}
