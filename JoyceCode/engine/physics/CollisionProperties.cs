using System;
using System.Text.Json.Serialization;

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
    [JsonInclude]
    public string Name;
    public string DebugInfo;
    [JsonInclude]
    public CollisionFlags Flags = CollisionFlags.IsDetectable;
    
    /**
     * Layers by convention are:
     * - 0x0001 : The player
     * - 0x0002 : Other characters that interact with each other
     * - 0x0004 : Other characters that interact with the player
     */
    [JsonInclude]
    public ushort LayerMask = 0xffff;
}