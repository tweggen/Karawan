namespace engine.quest;

public interface IQuest
{
    string GetTitle();
    string GetDescription();

    /**
     * Is the quest active right now?
     */
    bool IsActive();
    
    /**
     * Trigger the quest to be actually active.
     */
    void QuestActivate();
    
    /**
     * Deactivate the quest.
     */
    void QuestDeactivate();
}