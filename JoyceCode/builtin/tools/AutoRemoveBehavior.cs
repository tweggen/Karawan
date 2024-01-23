using System;
using engine.behave;
using engine.physics;

namespace Joyce.builtin.tools;

public class AutoRemoveBehavior : ABehavior
{
    public required float Lifetime { get; set; } = 1.0f;


    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        Lifetime -= dt;
        if (Lifetime <= 0)
        {
            entity.Dispose();
        }
    }
}