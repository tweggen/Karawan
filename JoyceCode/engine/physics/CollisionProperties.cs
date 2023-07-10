namespace engine.physics;

public class CollisionProperties
{
    public DefaultEcs.Entity Entity;
    public string Name;
    public string DebugInfo;
    public bool IsDetectable = true;
    public bool IsTangible = false;
    // Friction coefficient
    // maximum recovery velocity
    // spring setting
}