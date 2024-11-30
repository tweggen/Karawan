namespace engine.world.components;


/**
 * Describes the origin of this entity. Used for serialization.
 */
public struct Owner
{
    public const ushort OwnerId_Fragment = 0;
    public const ushort OwnerId_HardcodeMax = 511;
    
    /**
     * In the scope of the owning module, which id does this have?
     */
    public int Id;
    
    /**
     * The owner of an entity is responsible for deleting it.
     */
    public ushort OwnerId;

    public ushort _unused;

    public override string ToString()
    {
        return $"Id: {Id}, OwnerId: {OwnerId}";
    }

    public Owner(int id)
    { 
        Id = id;
    }
}