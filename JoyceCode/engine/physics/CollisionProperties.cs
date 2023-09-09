using System;

namespace engine.physics;

public class CollisionProperties
{
    [Flags]
    public enum CollisionFlags:uint {
        IsDetectable = 1,
        IsTangible = 2,
        TriggersCallbacks = 4
            
    };
    public DefaultEcs.Entity Entity;
    public string Name;
    public string DebugInfo;
    public CollisionFlags Flags = CollisionFlags.IsDetectable;
}