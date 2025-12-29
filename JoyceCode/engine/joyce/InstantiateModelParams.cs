using engine.geom;
using engine.physics;

namespace engine.joyce;

public class InstantiateModelParams
{
    public static int CENTER_X = 0x0001;
    public static int CENTER_Y = 0x0002;
    public static int CENTER_Z = 0x0004;
    public static int CENTER_X_POINTS = 0x0010;
    public static int CENTER_Y_POINTS = 0x0020;
    public static int CENTER_Z_POINTS = 0x0040;
    public static int ROTATE_X90 = 0x0100;
    public static int ROTATE_Y90 = 0x0200;
    public static int ROTATE_Z90 = 0x0400;
    public static int ROTATE_X180 = 0x1000;
    public static int ROTATE_Y180 = 0x2000;
    public static int ROTATE_Z180 = 0x4000;
    public static int REQUIRE_ROOT_INSTANCEDESC = 0x10000;

    /*
     * With this flag the model builder omits creation of an
     * additional root aentity to ensure a transform component
     * can be added to the entity to have it
     * conttrolled.
     */
    // public static int NO_CONTROLLABLE_ROOT = 0x20000;

    /**
     * Have the builder create physics as well
     */
    public static int BUILD_PHYSICS = 0x80000;

    /**
     * Have the physics be detectable by means of collision detection etc.
     */
    public static int PHYSICS_DETECTABLE = 0x100000;

    /**
     * Have the physics be tangible by means of collision detection etc.
     */
    public static int PHYSICS_TANGIBLE = 0x100000;

    /**
     * Are the physics intended to be static (as opposed to kinematic or dynamic)?
     */
    public static int PHYSICS_STATIC = 0x200000;
    
    /**
     * Shall we trigger callbacks?
     */
    public static int PHYSICS_CALLBACKS = 0x400000;
    
    /**
     * Shall we trigger callbacks?
     */
    public static int PHYSICS_OWN_CALLBACKS = 0x800000;

    /**
     * Flags to adjust the geometry of the model.
     * Note, that the geometry is adjusted before any other modifications
     * to the model.
     */
    public int GeomFlags { get; set; } = 0;
    
    
    public float MaxDistance
    {
        set
        {
            MaxVisibilityDistance = value;
            MaxBehaviorDistance = value;
            MaxPhysicsDistance = value;
            MaxAudioDistance = value;
        }
    }
    

    public float MaxVisibilityDistance { get; set; } = 10f;
    public float MaxBehaviorDistance { get; set; } = 10f;
    public float MaxPhysicsDistance { get; set; } = 10f;
    public float MaxAudioDistance { get; set; } = 10f;

    public CollisionProperties.Layers SolidLayerMask { get; set; } = CollisionProperties.Layers.NpcCharacter;
    public CollisionProperties.Layers SensitiveLayerMask { get; set; } = CollisionProperties.Layers.NpcCharacterSensitive;

    public AABB? PhysicsAABB { get; set; } = null;

    
    /**
     * THe name for this entity. This can be used to identify
     * e.g. quest specific objects or characters.
     */
    public string Name { get; set; } = "";
    
    public string Hash()
    {
        return $"{{\"geomFlags\": {GeomFlags}, \"maxDistance\": {MaxVisibilityDistance} }}";
    }
}