
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine.news;
using engine.quest.components;
using engine.world;
using static engine.Logger;

namespace engine.quest;


public class Manager : ObjectFactory<string, IQuest>, ICreator
{
    private Engine _engine;
    private Object _lo = new();
    private SortedDictionary<string, IQuest> _mapLoadedQuests = new();
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

        }
        catch (Exception e)
        {
            Error($"Exception activating quest: {e}.");
        }

    }
    
    
    /**
     * Create a quest.
     * This creates an entity and the quest object required to activate the quest.
     */
    private async Task<IQuest> _createQuest(string questName)
    {
        var quest = Get(questName); 
        if (null == quest)
        {
            ErrorThrow<ArgumentException>($"Requested to start unknown quest {questName}");
        }

        if (quest.Name != questName)
        {
            ErrorThrow<InvalidOperationException>($"Error while execution: Quest has wrong name.");
        }

        lock (_lo)
        {
            _mapLoadedQuests[quest.Name] = quest;
        }

        await I.Get<Engine>().TaskMainThread(() =>
        {
            var eExistingQuest = I.Get<Engine>().GetEcsWorld().GetEntities().With<components.Quest>()
                .AsEnumerable().FirstOrDefault(e => e.Get<components.Quest>().ActiveQuest?.Name == questName);

            DefaultEcs.Entity e;
            
            if (eExistingQuest.IsAlive)
            {
                Error($"Quest {questName} already exists. Using existing entity.");
                var cExistingQuest = eExistingQuest.Get<components.Quest>();
                if (cExistingQuest.ActiveQuest != quest)
                {
                    Error($"Existing quest {cExistingQuest.ActiveQuest.Name} was different to our quest {quest.Name}.");
                    if (quest.IsActive)
                    {
                        Error($"Exiting quest {quest.Name} was found active, so it probably should be activated.");
                    }
                } 
                e = eExistingQuest;
            }
            else
            {
                e = I.Get<Engine>().CreateEntity(quest.Name);
            }

            e.Set(new engine.quest.components.Quest() { ActiveQuest = quest });
            e.Set(new engine.world.components.Creator()
            {
                CreatorId = I.Get<CreatorRegistry>().FindCreatorId(this),
                Id = 0
            });
        });

        await quest.CreateEntities();
        
        return quest;
    }


    private async Task<IQuest> _findQuest(string questName)
    {
        IQuest quest;
        lock (_lo)
        {
            if (_mapLoadedQuests.TryGetValue(questName, out quest))
            {
                return quest;
            }
        }

        // TXWTODO: Yes, this obviously is a race condition.
        return await _createQuest(questName);
    }
    
    
    public async Task TriggerQuest(string questName, bool activate)
    {  
        var quest = await _findQuest(questName);
        if (activate)
        {
            if (!quest.IsActive)
            {
                ActivateQuest(quest);
            }
            else
            {
                Warning($"Triggering a quest that already was active.");
            }
        }
    }
    

    public void DeactivateQuest(IQuest quest)
    {
        string questName = quest.Name;
        lock (_lo)
        {
            if (!_mapLoadedQuests.ContainsKey(questName))
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
    public async Task<Action> SetupEntityFrom(Entity eLoaded, JsonElement je) => (() =>
    {
        /*
         * We are called because we are the creator. However, we want to delegate that call
         * to allow the individual quest to initialize itself-
         */
        /*
         * Note that we are right now in the logical thread.
         */
        try
        {
            //engine.quest.components.Quest cQuest;
            // IQuest iQuest = default;
            
            /*
             * The entity does have a Quest object with a quest instantiated. Load that.
             */
            engine.quest.components.Quest cQuest = eLoaded.Get<engine.quest.components.Quest>();
            IQuest? iQuest = cQuest.ActiveQuest;

            if (null == iQuest)
            {
                ErrorThrow<InvalidOperationException>($"Found null quest.");
            }

            lock (_lo)
            {
                // TXWTODO: Check if it existed.
                _mapLoadedQuests[iQuest.Name] = iQuest;
            }

            /*
             * So if the quest is a creator by itself, ask it to initialize.
             */
            ICreator iCreator = iQuest as ICreator;
            if (iCreator != null)
            {
                _engine.Run(async () =>
                {
                    var setupAction = await iCreator.SetupEntityFrom(eLoaded, je);

                    _engine.QueueMainThreadAction(
                        () =>
                        {
                            setupAction();
                            
                            /*
                             * Finally, if the quest is marked active, activate it now.
                             */
                            if (iQuest.IsActive)
                            {
                                ActivateQuest(iQuest);
                            }
                        });
                });
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
        // var cQuest = eLoader.Get<engine.quest.components.Quest>();
        jn = new JsonObject();
    }


    private void _loadQuests(JsonNode jnQuests)
    {
        try
        {
            if (jnQuests is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    try
                    {
                        string questName = pair.Key;
                        this.RegisterFactory(
                            questName,
                            _ => I.Get<engine.casette.Loader>().CreateFactoryMethod(pair.Key, pair.Value)()
                                as engine.quest.IQuest
                        );
                    }
                    catch (Exception e)
                    {
                        Warning($"Error setting global setting {pair.Key}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading global settings: {e}");
        }
    }

    
    private void _whenLoaded(string path, JsonNode? jn)
    {
        if (jn != null)
        {
            _loadQuests(jn);
        }
    }


    public Manager()
    {
        _engine = I.Get<Engine>();
        I.Get<CreatorRegistry>().RegisterCreator(this);
        I.Get<engine.casette.Loader>().WhenLoaded("/quests", _whenLoaded);
    }
}

