using System;

namespace engine.physics;

public class CollisionProperties
{
    [Flags]
    public enum CollisionFlags:ushort {
        IsDetectable = 1,
        IsTangible = 2,
        TriggersCallbacks = 4
    };
    public DefaultEcs.Entity Entity;
    public string Name;
    public string DebugInfo;
    public CollisionFlags Flags = CollisionFlags.IsDetectable;
    
    /**
     * Layers by convention are:
     * - 0x0001 : The player
     * - 0x0002 : Other characters that interact with each other
     * - 0x0004 : Other characters that interact with the player
     */
    public ushort LayerMask = 0xffff;
}