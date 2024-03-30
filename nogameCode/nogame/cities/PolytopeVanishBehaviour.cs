using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.physics;

namespace nogame.cities;

public class PolytopeVanishBehaviour : ABehavior
{
    private static float POLYTOPE_VANISH_TIME = 0.9f;
    private static float POLYTOPE_WOBBLE_FACTOR = 0.1f;
    private static float POLYTOPE_N_WOBBLES = 3f;
    private static float POLYTOPE_MIN_SCALE = 0.2f;

    public required Engine Engine;
    

    private float _lifetime = 0f;
    private bool _isDoomed = false;

    
    public override void Behave(in Entity entity, float dt)
    {
        _lifetime += dt;
        float time = _lifetime / POLYTOPE_VANISH_TIME;
        if (!_isDoomed && time > 1f)
        {
            _isDoomed = true;
            entity.Disable();
            Engine.AddDoomedEntity(entity);
            return;
        }

        float size = 1f - time * (1f - POLYTOPE_MIN_SCALE) + Single.Sin(time*2f*Single.Pi*POLYTOPE_N_WOBBLES) * POLYTOPE_WOBBLE_FACTOR;
        entity.Get<engine.joyce.components.Transform3>().Scale = size * Vector3.One;
        entity.Get<engine.joyce.components.Transform3ToParent>().Transform3 =
            entity.Get<engine.joyce.components.Transform3>();
    }
}