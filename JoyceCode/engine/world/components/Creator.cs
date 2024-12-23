namespace engine.world.components;

[engine.IsPersistable]
public struct Creator
{
    public const ushort CreatorId_Hardcoded = 0;
    public const ushort CreatorId_HardcodeMax = 511;
    
    /**
     * In the scope of the creating unit, which id does this have?
     * This may stay constant across saves.
     */
    public int Id { get; set; }
    
    /**
     * The owner of an entity is responsible for deleting it.
     */
    public ushort CreatorId { get; set; }
    
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