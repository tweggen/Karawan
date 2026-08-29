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


    /**
     * What the world has always used, and what anything that does not say otherwise
     * keeps using: rubber on dry asphalt, near enough.
     *
     * It is the right number for a body with wheels or feet and the wrong one for
     * anything that is meant to graze a surface rather than grip it.
     */
    public const float DefaultFriction = 1f;


    /**
     * Coefficient of friction this body brings to a contact.
     *
     * Per body rather than per pair, because the interesting cases are all a property
     * of ONE participant: a hover vehicle has no wheels and should slide off whatever
     * it touches, and what it happens to touch - a road, a kerb, a wall, a pedestrian -
     * does not change that.
     *
     * Lowering it does not make a body less solid. Contacts are still generated and
     * still resolved; only the tangential part of the resolution is weakened, so the
     * body slides along the surface instead of being held to it.
     */
    [JsonInclude] public float Friction = DefaultFriction;


    /**
     * The coefficient a contact between two bodies is resolved with.
     *
     * The LOWER of the two, so that a body which declares itself slippery is slippery
     * against everything, which is the whole reason a body would declare it. Averaging
     * or multiplying would let the surface argue, and then a hover ship would grip
     * exactly the high friction road surface it most needs not to.
     */
    public static float CombineFriction(float a, float b) => Single.Min(a, b);
}