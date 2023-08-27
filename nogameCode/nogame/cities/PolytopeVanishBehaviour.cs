using System;
using System.Numerics;
using DefaultEcs;
using engine;

namespace nogame.cities;

public class PolytopeVanishBehaviour : IBehavior
{
    private static float POLYTOPE_VANISH_TIME = 1.0f;
    private static float POLYTOPE_WOBBLE_FACTOR = 0.2f;
    private static float POLYTOPE_N_WOBBLES = 2f;
    private static float POLYTOPE_MIN_SCALE = 0.4f;

    private float _lifetime = 0f;
    
    public void Behave(in Entity entity, float dt)
    {
        _lifetime += dt;
        float time = _lifetime / POLYTOPE_VANISH_TIME;
        if (time > 1f)
        {
            entity.Dispose();
            return;
        }

        float size = 1f - time * (1f - POLYTOPE_MIN_SCALE) + Single.Sin(time*2f*Single.Pi*POLYTOPE_N_WOBBLES) * POLYTOPE_WOBBLE_FACTOR;
        entity.Get<engine.transform.components.Transform3>().Scale = size * Vector3.One;
        entity.Get<engine.transform.components.Transform3ToParent>().Transform3 =
            entity.Get<engine.transform.components.Transform3>();
    }
}