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
    public override string GetStartStrategy() => "pickup";


    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["pickup"])
        {
            TriggerStrategy("driving");
        }
        else if (strategy == Strategies["driving"])
        {
            TriggerStrategy(null);
            I.Get<engine.quest.QuestFactory>().DeactivateQuest(_entity);
        }
    }


    public TaxiQuestStrategy(Vector3 guestPosition, Vector3 destinationPosition)
    {
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
