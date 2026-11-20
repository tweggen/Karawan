using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using engine;
using engine.joyce.components;
using engine.world.components;
using static engine.Logger;

namespace nogame.quests.HelloFishmonger;


/**
 * Owns HelloFishmongerQuestCreator, so a HelloFishmonger chase survives a save (KAR-475).
 *
 * Creator ids are handed out in registration order from Creator.CreatorId_HardcodeMax + 1
 * and saves store the id, so the order must not change between sessions: the taxi's
 * creator came first and has to keep its id. Depending on TaxiNpcSpawnerModule makes it
 * activate - and register - before this module does.
 *
 * It also clears what saves written before this carry: the car itself, stored with a
 * Creator of its own and coming back with no model, no body and no quest.
 */
public class HelloFishmongerModule : AModule
{
    public HelloFishmongerQuestCreator QuestCreator { get; private set; }


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.quests.Taxi.TaxiNpcSpawnerModule>()
    };


    /*
     * The car is never saved any more, so a car loaded with the hardcoded creator is one
     * of those older shells. Nothing refers to it: the quest that chased it was not saved.
     */
    private void _onAfterLoadGame(object sender, object gameState)
    {
        _engine.QueueMainThreadAction(() =>
        {
            var shells = _engine.GetEcsWorld().GetEntities()
                .With<Creator>().With<EntityName>()
                .AsEnumerable()
                .Where(e => e.Get<Creator>().CreatorId == Creator.CreatorId_Hardcoded
                            && e.Get<EntityName>().Name == FishmongerCar.Name)
                .ToList();
            foreach (var eShell in shells)
            {
                var e = eShell;
                Trace($"HelloFishmonger: removing a quest car stored by an older save.");
                I.Get<engine.joyce.HierarchyApi>().Delete(ref e);
            }
        });
    }


    protected override void OnModuleDeactivate()
    {
        I.Get<engine.Saver>().OnAfterLoadGame -= _onAfterLoadGame;
        I.Get<engine.world.CreatorRegistry>().UnregisterCreator(QuestCreator);
    }


    protected override void OnModuleActivate()
    {
        QuestCreator = new HelloFishmongerQuestCreator();
        I.Get<engine.world.CreatorRegistry>().RegisterCreator(QuestCreator);
        I.Get<engine.Saver>().OnAfterLoadGame += _onAfterLoadGame;
    }
}
