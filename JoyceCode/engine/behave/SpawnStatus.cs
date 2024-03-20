using BepuPhysics.Constraints;

namespace engine.behave;


public class SpawnStatus
{
    private static int _nextId = 0;
 
    /**
     * For debugging: Identify this SpawnStatus
     */
    public int Id;
    
    /**
     * The minimum of characters per fragment.
     */
    public ushort MinCharacters;

    /**
     * The maximum of characters per fragment.
     */
    public ushort MaxCharacters;

    /**
     * The number of characters in creation.
     */
    public ushort InCreation;
    
    /**
     * The number of characters currently being killed.
     */
    public ushort IsDying;
    
    /**
     * The number of deads that won't be recreated
     */
    public ushort Dead;
    

    public ushort ResidentCharacters
    {
        get => (ushort)(InCreation + Dead);
    }


    public bool IsValid()
    {
        return InCreation != 0xffff;
    }

    public SpawnStatus()
    {
        InCreation = 0xffff;
        Id = ++_nextId;
    }
}