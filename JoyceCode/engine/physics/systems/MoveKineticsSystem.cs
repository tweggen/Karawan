using BepuPhysics;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DefaultEcs;
using static engine.Logger;

namespace engine.physics.systems;

/**
     * Read the world transform position of the entities and set it to the kinetics.
     */
[DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(engine.physics.components.Kinetic))]
internal class MoveKineticsSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    private DefaultEcs.Entity _ePlayer;
    private Matrix4x4 _mPlayerTransform;
    private Vector3 _vPlayerPos;
    
    private bool _havePlayerPosition = false;

    static private Vector3 _vOffPosition = new(0f, -3000f, 0f);

    private engine.transform.API _aTransform;

    protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        foreach (var entity in entities)
        {
            var cKinetic = entity.Get<physics.components.Kinetic>();
            var oldPos = cKinetic.LastPosition;
            var newPos = entity.Get<transform.components.Transform3ToWorld>().Matrix.Translation;

            bool isInside = Vector3.DistanceSquared(newPos, _vPlayerPos) <= cKinetic.MaxDistance * cKinetic.MaxDistance; 
            bool wasInside = Vector3.DistanceSquared(oldPos, _vPlayerPos) <= cKinetic.MaxDistance * cKinetic.MaxDistance; 

            var bodyReference = cKinetic.Reference;

            if (isInside)
            {
                // TXWTODO: Can we write that more efficiently?
                if (oldPos != newPos)
                {
                    bodyReference.Pose.Position = newPos;
                    entity.Get<physics.components.Kinetic>().LastPosition = newPos;
                    if (oldPos != Vector3.Zero)
                    {
                        Vector3 vel = (newPos - oldPos) / dt;
                        bodyReference.Velocity.Linear = vel;
                    }
                    else
                    {
                        bodyReference.Velocity.Linear = Vector3.Zero;
                    }
                    bodyReference.Awake = true;
                }
            }
            else
            {
                if (wasInside || true==bodyReference.Awake)
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

    protected override void PostUpdate(float dt)
    {

    }


    protected override void PreUpdate(float dt)
    {
        _aTransform = _engine.GetATransform();
        _havePlayerPosition = false;
        _ePlayer = _engine.GetPlayerEntity();
        if (_ePlayer.IsAlive && _ePlayer.IsEnabled())
        {
            if (_ePlayer.Has<engine.transform.components.Transform3ToWorld>())
            {
                _mPlayerTransform = _ePlayer.Get<engine.transform.components.Transform3ToWorld>().Matrix;
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