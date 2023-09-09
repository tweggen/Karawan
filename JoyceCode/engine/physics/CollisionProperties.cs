using System;

namespace engine.physics;

public class CollisionProperties
{
    [Flags]
    public enum CollisionFlags:uint {
        IS_DETECTABLE = 1,
        IS_TANGIBLE = 2,
        TRIGGERS_CALLBACKS = 4
            
    };
    public DefaultEcs.Entity Entity;
    public string Name;
    public string DebugInfo;
    public CollisionFlags Flags;
    // Friction coefficient
    // maximum recovery velocity
    // spring setting
}