using System;
using System.Text.Json.Serialization;
using OneOf.Types;

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
        Player = 0x0003,
        
        PlayerMelee = 0x0004,
        PlayerBullet = 0x0008,
        PlayerWeapon = 0x000c,
        
        NpcCharacter = 0x0010,
        NpcVehicle = 0x0020,
        Npc = 0x0030,
        
        NpcMelee = 0x0040,
        NpcBullet = 0x0080,
        NpcWeapon = 0x00c0,
        
        PlayerSensitive = 
            Npc 
            | NpcWeapon 
            
            | Terrain
            | StaticEnvironment 
            | MovableEnvironment
            | Collectable 
            | QuestMarker,
        
        /**
         * The usual npc is controlled and does not interact with the environment,
         * but with other vehicles and armory of the player.
         */
        NpcCharacterSensitive = PlayerVehicle | PlayerMelee | PlayerBullet | NpcVehicle, 
        
        AnyVehicle = PlayerVehicle | NpcVehicle,
        AnyWeapon = PlayerWeapon | NpcWeapon,
        
        Terrain = 0x0100,
        StaticEnvironment = 0x0200,
        MovableEnvironment = 0x0400,
        Collectable = 0x0800,
        QuestMarker = 0x1000,
        
        All = 0xffff
    }

    /**
     * This is the mask of layers I am part of.
     */
    [JsonInclude] public Layers SolidLayerMask = Layers.All;

    /**
     * This is the mask of layers I am sensitive to.
     */
    [JsonInclude] public Layers SensitiveLayerMask = Layers.All;
}