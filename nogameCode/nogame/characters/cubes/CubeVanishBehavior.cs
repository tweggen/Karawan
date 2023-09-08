using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.physics;

namespace nogame.characters.cubes;

public class CubeVanishBehavior : IBehavior
{
    public required Engine Engine;
    private static float CUBE_VANISH_TIME = 0.4f;
    private static float POLYTOPE_MIN_SCALE = 0.1f;

    private float _lifetime = 0f;


    public void OnCollision(ContactEvent cev)
    {
    }

    public void Sync(in Entity entity)
    {
    }

    public void Behave(in Entity entity, float dt)
    {
        _lifetime += dt;
        float time = _lifetime / CUBE_VANISH_TIME;
        if (time > 1f)
        {
            Engine.AddDoomedEntity(entity);
            return;
        }

        float size = 1f - time * (1f - POLYTOPE_MIN_SCALE);
        entity.Get<engine.transform.components.Transform3>().Scale = size * Vector3.One;
        entity.Get<engine.transform.components.Transform3ToParent>().Transform3 =
            entity.Get<engine.transform.components.Transform3>();
    }
}
