using System;
using System.Numerics;
using static engine.Logger;

namespace engine.physics.systems;

/**
 * Read the world transform position of the entities and set it to the kinetics.
 */
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(engine.physics.components.Body))]
internal class MoveKineticsSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    private DefaultEcs.Entity _ePlayer;
    private Matrix4x4 _mPlayerTransform;
    private Vector3 _vPlayerPos;
    
    private bool _havePlayerPosition = false;

    static private Vector3 _vOffPosition = new(0f, -3000f, 0f);

    private engine.joyce.TransformApi _aTransform;

    protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        foreach (var entity in entities)
        {
            ref physics.components.Body cRefKinetic = ref entity.Get<physics.components.Body>();
            var po = cRefKinetic.PhysicsObject;
            if (po == null) continue;
            
            // TXWTODO: This is a workaround for addressing only the former kinematic objects.
            if ((po.Flags & (physics.Object.HaveContactListener|physics.Object.IsStatic)) != 0)
            {
                continue;
            }
            
            lock (_engine.Simulation)
            {
                var oldPos = po.LastPosition;
                var newPos = entity.Get<joyce.components.Transform3ToWorld>().Matrix.Translation;

                float maxDistance2 = po.MaxDistance * po.MaxDistance;
                bool isInside = Vector3.DistanceSquared(newPos, _vPlayerPos) <= maxDistance2;
                bool wasInside = Vector3.DistanceSquared(oldPos, _vPlayerPos) <= maxDistance2;

                var bodyReference = cRefKinetic.Reference;

                if (isInside)
                {
                    // TXWTODO: Can we write that more efficiently?
                    if (oldPos != newPos)
                    {
                        bodyReference.Pose.Position = newPos;
                        po.LastPosition = newPos;
                        if (oldPos != Vector3.Zero)
                        {
                            Vector3 vel = (newPos - oldPos) / dt;
                            bodyReference.Velocity.Linear = vel;
                        }
                        else
                        {
                            bodyReference.Velocity.Linear = Vector3.Zero;
                        }

                        if (!bodyReference.Awake)
                        {
                            bodyReference.Awake = true;
                        }
                    }
                }
                else
                {
                    if (wasInside || true == bodyReference.Awake)
                    {
                        /*
                         * If it previously was inside, reposition it to nowehere
                         * and make it passive. effectively setting it to standby.
                         */
                        bodyReference.Pose.Position = _vOffPosition;
                        bodyReference.Pose.Orientation = Quaternion.Identity;
                        bodyReference.Velocity.Linear = Vector3.Zero;
                        bodyReference.Velocity.Angular = Vector3.Zero;
                        bodyReference.Awake = false;
                    }

                    /*
                     * And if it already was outside, we do not need to touch it in any way.
                     */
                }
            }
        }
    }

    protected override void PostUpdate(float dt)
    {

    }


    protected override void PreUpdate(float dt)
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();
        _havePlayerPosition = false;
        _ePlayer = _engine.GetPlayerEntity();
        if (_ePlayer.IsAlive && _ePlayer.IsEnabled())
        {
            if (_ePlayer.Has<engine.joyce.components.Transform3ToWorld>())
            {
                _mPlayerTransform = _ePlayer.Get<engine.joyce.components.Transform3ToWorld>().Matrix;
                _vPlayerPos = _mPlayerTransform.Translation;
                _havePlayerPosition = true;
            }
        }
    }

    
    public MoveKineticsSystem(in engine.Engine engine)
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
    }
}