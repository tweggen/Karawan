namespace engine.quest;

public class Description
{
    public string Title { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
}

public interface IQuest : IModule
{
    Description GetDescription();

    /**
     * Is the quest active right now?
     */
    bool IsActive();
}