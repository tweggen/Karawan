using System.Threading.Tasks;

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
    
    /*
     * Prepare the world for the quest and start it.
     * If created within the game, this is the way to create
     * the quest.
     *
     * If deserilizating the quest from disk, we define that the world
     * shall contain everything that is required by just recreating all
     * entities and the corresponding quest object.
     *
     * In other words: Everything that is initialited in prepare
     * must be inside entities later on.
     */
    // static IQuest Instantiate()

    
    /**
     * Create everything that is reflected in entities if the quest is
     * not restored from disk.
     */
    public Task CreateEntities();
}


