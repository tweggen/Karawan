namespace engine.world.components;

public struct Creator
{
    public const ushort CreatorId_Hardcoded = 0;
    public const ushort CreatorId_HardcodeMax = 511;
    
    /**
     * In the scope of the creating unit, which id does this have?
     * This may stay constant across saves.
     */
    public int Id;
    
    /**
     * The owner of an entity is responsible for deleting it.
     */
    public ushort CreatorId;
    
    /**
     * The create of an entity is responsible for setting it up
     * from serializable data.
     */
    public ushort _unused;

    public override string ToString()
    {
        return $"CreatorId: {CreatorId}";
    }

    public Creator(ushort creatorId)
    {
        CreatorId = creatorId;
    }

}