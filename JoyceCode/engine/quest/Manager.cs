
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine.news;
using engine.world;
using static engine.Logger;

namespace engine.quest;


public class Manager : ObjectFactory<string, IQuest>, ICreator  
{
    private Object _lo = new();
    private SortedDictionary<string, IQuest> _mapOpenQuests = new();
    // private List<string> _listAvailableQuests = new();

    
    private string _questEntityName(string questName)
    {
        return $"quest {questName}";
    }


    public override void RegisterFactory(string name, Func<string, IQuest> factory)
    {
        // _listAvailableQuests.Add(name);
        base.RegisterFactory(name, factory);
    }


    public void ActivateQuest(IQuest quest)
    {
        try
        {
            quest.IsActive = true;
            quest.ModuleActivate();
            lock (_lo)
            {
                _mapOpenQuests.Add(quest.Name, quest);
            }

            I.Get<Engine>().QueueEntitySetupAction(_questEntityName(quest.Name), e =>
            {
                e.Set(new engine.quest.components.Quest() { ActiveQuest = quest });
                e.Set(new engine.world.components.Creator()
                {
                    CreatorId = I.Get<CreatorRegistry>().FindCreatorId(this),
                    Id = 0
                });
            });
        }
        catch (Exception e)
        {
            Error($"Exception activating quest: {e}.");
        }

    }
    
    
    public void ActivateQuest(string questName)
    {
        engine.quest.IQuest quest = Get(questName);
        if (null == quest)
        {
            ErrorThrow<ArgumentException>($"Requested to start unknown quest {questName}");
        }

        if (quest.Name != questName)
        {
            ErrorThrow<InvalidOperationException>($"Error while execution: Quest has wrong name.");
        }

        ActivateQuest(quest);
    }

    
    public void DeactivateQuest(IQuest quest)
    {
        string questName = quest.Name;
        lock (_lo)
        {
            if (!_mapOpenQuests.ContainsKey(questName))
            {
                Error($"Asked to close quest that had not been open.");
                return;
            }
        }
        
        quest.IsActive = false;
        quest.ModuleDeactivate();
        
        /*
         * Finally, remove the quest from the entity database.
         * Note, that we do not dispose the quest object.
         */
        I.Get<Engine>().QueueMainThreadAction(() =>
        {
            DefaultEcs.Entity eQuest = I.Get<Engine>().GetEcsWorld().GetEntities()
                .With<engine.quest.components.Quest>()
                .With<engine.joyce.components.EntityName>()
                .AsEnumerable()
                .FirstOrDefault(e => e.Get<engine.joyce.components.EntityName>().Name == _questEntityName(questName));

            eQuest.Dispose();
            
            I.Get<Saver>().Save("quest deactivated");
        });
    }


    /**
     * Called after all entities are initialized. Called because of the Creator component
     * in the entity.
     */
    public Func<Task> SetupEntityFrom(Entity eLoaded, JsonElement je) => (async () =>
    {
        /*
         * We are called because we are the creator. However, we want to delegate that call
         * to allow the individual quest to initialize itself-
         */

        try
        {
            /*
             * The entity does have a Quest object with a quest instantiated. Load that.
             */
            // TXWTODO: This is suppsed to be in the logical thread.
            engine.quest.components.Quest cQuest = eLoaded.Get<engine.quest.components.Quest>();
            var quest = cQuest.ActiveQuest;
            if (null == quest)
            {
                ErrorThrow<InvalidOperationException>($"Found null quest.");
            }

            /*
             * So if the quest is a creator by itself, ask it to initialize.
             */
            ICreator iCreator = quest as ICreator;
            if (iCreator != null)
            {
                await iCreator.SetupEntityFrom(eLoaded, je)();
            }

            if (quest.IsActive)
            {
                ActivateQuest(quest);
            }
        }
        catch (Exception e)
        {
            Error($"Error while forwarding setup call to quest: {e}");
        }

        return;
    });


    /**
     * We register this manager as the creator of quests. We use this to
     * assign the entity properly to the quest objects, which might need
     * to be factored.
     */
    public void SaveEntityTo(Entity eLoader, out JsonNode jn)
    {
        var cQuest = eLoader.Get<engine.quest.components.Quest>();
        jn = new JsonObject();
    }


    public Manager()
    {
        I.Get<CreatorRegistry>().RegisterCreator(this);
    }
}

