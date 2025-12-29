using System;
using System.Text.Json.Serialization;

namespace engine.physics;

public class CollisionProperties
{
    [Flags]
    public enum CollisionFlags : ushort
    {
        IsDetectable = 1,
        IsTangible = 2,
        TriggersCallbacks = 4
    };

    public DefaultEcs.Entity Entity;
    [JsonInclude] public string Name;
    public string DebugInfo;
    [JsonInclude] public CollisionFlags Flags = CollisionFlags.IsDetectable;

    [Flags]
    public enum Layers
    {
        PlayerCharacter = 0x0001,
        PlayerVehicle = 0x0002,
        PlayerWeapon = 0x0004,
        PlayerBullet = 0x0008,
        NpcCharacter = 0x0010,
        NpcVehicle = 0x0020,
        NpcWeapon = 0x0040,
        NpcBullet = 0x0080, 
        Terrain = 0x0100,
        StaticEnvironment = 0x0200,
        MovableEnvironment = 0x0400,
        Collectables = 0x0800,
        QuestMarker = 0x1000
    }

    /**
     * Layers by convention are:
     * - 0x0001 : The player
     * - 0x0002 : Other characters that interact with each other
     * - 0x0004 : Other characters that interact with the player
     * - 0x0008 : Environment
     * - 0x0010 : Player weapon
     */
    [JsonInclude]
    public ushort LayerMask = 0xffff;
}