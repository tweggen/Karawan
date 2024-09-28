
using System;
using engine.news;
using static engine.Logger;

namespace engine.quest;


public class Manager : ObjectFactory<string, IQuest>
{
    private Object _lo = new();
    
    private IQuest? _currentQuest = null;
    
    public IQuest ActivateQuest(string questName)
    {
        engine.quest.IQuest quest = I.Get<engine.quest.Manager>().Get(questName);
        if (null == quest)
        {
            ErrorThrow<ArgumentException>($"Requested to start unknown quest {questName}");
        }

        quest.ModuleActivate();

        lock (_lo)
        {
            _currentQuest = quest;
        }
        
        return quest;
    }


    public void DeactivateQuest(IQuest quest)
    {
        lock (_lo)
        {
            if (quest != _currentQuest)
            {
                return;
            }

            _currentQuest = null;
        }
        
        quest.ModuleDeactivate();
        quest.Dispose();

        I.Get<EventQueue>().Push(new Event("builtin.SaveGame.TriggerSave", "questFinshed"));
    }
}

