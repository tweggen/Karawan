namespace builtin.modules.inventory;

using engine;

public class PickableDescription
{
    /**
     * Internal unique path to identify the pickable description.
     */
    public string Path;
    
    /**
     * Human readable non-translated name of the pickable.
     */
    public string Name;
    
    /**
     * Human readable non-translated description of the pickable.
     */
    public string Description;
    
    /**
     * The action to be excuted as soon the pickable is used.
     */
    public GameAction UseAction;
    
    
    public float Weight;
    public float Volume;
}