using BepuPhysics.Constraints;

namespace engine.behave;

public struct SpawnStatus
{
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
     * The number of deads that won't be recreated
     */
    public ushort Dead;


    public bool IsValid()
    {
        return InCreation != 0xffff;
    }

    public SpawnStatus()
    {
        InCreation = 0xffff;
    }
}