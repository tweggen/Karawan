using System.Collections.Generic;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;

namespace nogame.quests.HelloFishmonger;

public class HelloFishmongerStrategy : AOneOfStrategy
{
    public override string GetStartStrategy() => "trail";


    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["trail"])
        {
            TriggerStrategy(null);
            I.Get<nogame.modules.story.Narration>().TriggerNarration("firstPubSecEncounter", "");
            I.Get<engine.quest.QuestFactory>().DeactivateQuest(_entity);
        }
    }


    public HelloFishmongerStrategy(Entity eCarEntity)
    {
        Strategies = new()
        {
            {
                "trail", new TrailStrategy()
                {
                    Controller = this,
                    CarEntity = eCarEntity
                }
            }
        };
    }
}
