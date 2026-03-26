namespace engine.tale.components;

/// <summary>
/// Tags an ECS entity with the NPC ID from the TALE system.
/// Enables lookup from DefaultEcs.Entity to NpcSchedule during dematerialization.
/// </summary>
public struct TaleNpcId
{
    public int NpcId;
}
