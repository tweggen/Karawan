
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
    private List<string> _listAvailableQuests = new();

    
    private string _questEntityName(string questName)
    {
        return $"quest {questName}";
    }


    public override void RegisterFactory(string name, Func<string, IQuest> factory)
    {
        _listAvailableQuests.Add(name);
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

        try
        {
            quest.IsActive = true;
            quest.ModuleActivate();
            lock (_lo)
            {
                _mapOpenQuests.Add(questName, quest);
            }

            I.Get<Engine>().QueueEntitySetupAction(_questEntityName(questName), e =>
            {
                e.Set(new engine.quest.components.Quest() { ActiveQuest = quest });
                e.Set(new engine.world.components.Creator()
                {
                    CreatorId = I.Get<CreatorRegistry>().FindCreatorId(this),
                    Id = _listAvailableQuests.IndexOf(quest.Name)
                });
            });
        }
        catch (Exception e)
        {
            Error($"Exception activating quest: {e}.");
        }

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



    public Func<Task> SetupEntityFrom(Entity eLoaded, in JsonElement je) => new(async () =>
    {
        /*
         * We are called because we are the creator. However, we want to delegate that call.
         */
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

}

