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
    public bool AllowOffPosition { get; set; } = true;

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
            if ((po.Flags & (physics.Object.IsDynamic|physics.Object.IsStatic)) != 0)
            {
                continue;
            }

           
            lock (_engine.Simulation)
            {
                var oldPos = po.LastPosition;
                var newPos = entity.Get<joyce.components.Transform3ToWorld>().Matrix.Translation + po.BodyOffset;

                float maxDistance2 = po.MaxDistance * po.MaxDistance;
                bool isInside = Vector3.DistanceSquared(newPos, _vPlayerPos) <= maxDistance2;
                bool wasInside = Vector3.DistanceSquared(oldPos, _vPlayerPos) <= maxDistance2;
                bool isFirstPos = po.LastPosition == default;

                var bodyReference = cRefKinetic.Reference;

                if (isInside)
                {
                    // TXWTODO: Can we write that more efficiently?
                    bool hasNewPos = oldPos != newPos;
                    
                    /*
                     * No matter,
                     * - if the body has changed its target position
                     * - it was not inside before
                     * - it has a position for the first time
                     * we need to set the physics object's position, if it was outside before.
                     */
                    if (isFirstPos || !wasInside || hasNewPos)
                    {
                        if (po.CollisionProperties?.Name == "nogame.playerhover.person")
                        {
                            int a = 1;
                        }
                        actions.SetBodyPosePosition.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, newPos);
                        //actions.SetBodyAwake.Execute(_engine.PLog, _engine.Simulation, ref bodyReference,true);
                        po.LastPosition = newPos;
                    }
                        
                    /*
                     * wake it up if
                     * - it hasn't been inside before and has no new position, we need to wake itx^xup.
                     * - it hasn't been inside and is the first time
                     */
                    if (isFirstPos || !wasInside && !hasNewPos)
                    {
                        if (!bodyReference.Awake)
                        {
                            actions.SetBodyAwake.Execute(_engine.PLog, _engine.Simulation, ref bodyReference,true);
                        }
                    }
                    
                    /*
                     * If the position of the body has changed, we wamt to compute
                     * its velocity, remember it and transfer it to the kinemativ object.
                     */
                    if (hasNewPos)
                    {
                        if (true || bodyReference.Constraints.Count == 0)
                        {
                            if (oldPos != Vector3.Zero)
                            {
                                Vector3 vel = (newPos - oldPos) / dt;
                                actions.SetBodyLinearVelocity.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, vel);
                                if (!bodyReference.Awake)
                                {
                                    actions.SetBodyAwake.Execute(_engine.PLog, _engine.Simulation, ref bodyReference,true);
                                }
                            }
                            else
                            {
                                actions.SetBodyLinearVelocity.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, Vector3.Zero);
                            }
                        }
                        else
                        {
                            Error($"Avoiding to move body {bodyReference.Handle} while constraints apply.");
                        }
                    }
                    else
                    {
                    }
                }
                else
                {
                    if (AllowOffPosition)
                    {
                        if (wasInside)
                        {
                            if (bodyReference.Constraints.Count > 0)
                            {
                                // Error($"Moving away body with constraints.");
                            }
                            /*
                             * If it previously was inside, reposition it to nowehere
                             * and make it passive. effectively setting it to standby.
                             */
                            actions.SetBodyPosePosition.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, _vOffPosition);
                            actions.SetBodyPoseOrientation.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, Quaternion.Identity);
                            actions.SetBodyLinearVelocity.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, Vector3.Zero);
                            actions.SetBodyAngularVelocity.Execute(_engine.PLog, _engine.Simulation, ref bodyReference, Vector3.Zero);
                        }

                        /*
                         * And if it already was outside, we do not need to touch it in any way.
                         * Just make sure it sleeps.
                         */
                        if (bodyReference.Awake)
                        {
                            if (bodyReference.Constraints.Count == 0)
                            {
                                // bodyReference.Awake = false;
                                actions.SetBodyAwake.Execute(_engine.PLog, _engine.Simulation, ref bodyReference,false);
                            }
                            else
                            {
                                // Error($"Would have had a problem before.");
                            }
                        }
                    }

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
        if (_engine.Player.TryGet(out _ePlayer))
        {
            if (_ePlayer.Has<engine.joyce.components.Transform3ToWorld>())
            {
                _mPlayerTransform = _ePlayer.Get<engine.joyce.components.Transform3ToWorld>().Matrix;
                _vPlayerPos = _mPlayerTransform.Translation;
                _havePlayerPosition = true;
            }
        }
    }

    
    public MoveKineticsSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}