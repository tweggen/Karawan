namespace engine.quest;

public class Description
{
    public string Title { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
}

public interface IQuest : IModule
{
    /**
     * A quest name for internal operations. 
     */
    public string Name { get; set;  }
    
    public Description GetDescription();

    /**
     * Is the quest active right now?
     */
    public bool IsActive { get; set; }
    
    
    // TXWTODO: let the quest have a validation method on startup
    // public bool ValidateWorld();
}


