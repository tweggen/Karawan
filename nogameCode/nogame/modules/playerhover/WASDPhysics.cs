using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.draw;
using engine.geom;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

internal class WASDPhysics : AModule, IInputPart
{
    public static float MY_Z_ORDER = 25f;
    private DefaultEcs.Entity _eTarget;

    private BepuPhysics.BodyReference _prefTarget;

    private float _massShip;

    public float LinearThrust { get; set; } = 70f;
    public float AngularThrust { get; set; } = 50.0f;

    public float MaxLinearVelocity { get; set; } = 150f;
    public float MaxAngularVelocity { get; set; } = 0.8f;
    public float LevelUpThrust { get; set; } = 16f;
    public float LevelDownThrust { get; set; } = 16f;
    public float NoseDownWhileAcceleration { get; set; } = 1f;
    public float WingsDownWhileTurning { get; set; } = 4f;

    public float InputTurnThreshold { get; set; } = 50f;
    public float FirstInputTurnThreshold { get; set; } = 10f;


    private float _lastTurnMotion = 0f;


    private void _onLogicalFrame(object sender, float dt)
    {
        Vector3 vTotalImpulse = new Vector3(0f, 9.81f, 0f);

        Vector3 vTotalAngular = new Vector3(0f, 0f, 0f);

        /*
         * Balance height first.
         *
         * Create an impulse appropriate to work against the
         * gravity. Note, that the empty impulse is pre-set with
         * an impulse to accelerate against gravity.
         */
        Vector3 vTargetPos;
        Vector3 vTargetVelocity;
        Vector3 vTargetAngularVelocity;
        lock (_engine.Simulation)
        {
            vTargetPos = _prefTarget.Pose.Position;
            vTargetVelocity = _prefTarget.Velocity.Linear;
            vTargetAngularVelocity = _prefTarget.Velocity.Angular;     
        }
    

        /*
         * Keep player in bounds.
         */
        if (!MetaGen.AABB.Contains(vTargetPos))
        {
            lock (_engine.Simulation)
            {
                // vTargetPos = _prefTarget.Pose.Position = Vector3.Zero;
                _prefTarget.Pose.Orientation = Quaternion.Identity;
                _prefTarget.Velocity.Angular = Vector3.Zero;
                _prefTarget.Velocity.Linear = Vector3.Zero;
            }
            _eTarget.Set(new engine.joyce.components.Motion(_prefTarget.Velocity.Linear));

            return;
        }

        float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetNavigationHeightAt(vTargetPos);
        {
            var properDeltaY = 0;
            var deltaY = vTargetPos.Y - (heightAtTarget + properDeltaY);
            const float threshDiff = 0.01f;

            Vector3 impulse;
            float properVelocity = 0f;
            if (deltaY < -threshDiff)
            {
                properVelocity = LevelUpThrust; // 1ms-1 up.
            }
            else if (deltaY > threshDiff)
            {
                properVelocity = -LevelDownThrust; // 1ms-1 down.
            }

            float deltaVelocity = properVelocity - vTargetVelocity.Y;
            float fireRate = deltaVelocity;
            impulse = new Vector3(0f, fireRate, 0f);
            vTotalImpulse += impulse;
        }

        /*
         * Apply controls
         */
        // var cTransform3 = _eTarget.Get<engine.joyce.components.Transform3>();

        var cToParent = _eTarget.Get<engine.joyce.components.Transform3ToParent>();

        /*
         * We cheat a bit, reading the matrix for the direction matrix,
         * applying the position change to the transform parameters,
         * applying rotation directly to the transform parameters.
         */
        var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
        var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
        var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);

        /*
         * If I shall control the ship.
         */
        if (MY_Z_ORDER == I.Get<engine.news.InputEventPipeline>().GetFrontZ())
        {
            I.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);

            var frontMotion = controllerState.FrontMotion;
            var upMotion = controllerState.UpMotion;
            float turnMotion;
            {
                float inputTurnMotion = controllerState.TurnRight - controllerState.TurnLeft;
                
                /*
                 * invert control if we are going backwards. This is car like, not thrust like,
                 * but feels more natural for GTA infused people like me.
                 */
                if (Vector3.Dot(vFront, vTargetVelocity) < 0.1f)
                {
                    inputTurnMotion *= -1f;
                }
                
                float maxThreshold;
                if (_lastTurnMotion == 0)
                {
                    maxThreshold = FirstInputTurnThreshold;
                }
                else
                {
                    maxThreshold = InputTurnThreshold;
                }

                float diff = inputTurnMotion - _lastTurnMotion;
                if (Single.Abs(diff) > maxThreshold)
                {
                    turnMotion = _lastTurnMotion + Single.Sign(diff) * maxThreshold;
                }
                else
                {
                    turnMotion = inputTurnMotion;
                }

                _lastTurnMotion = turnMotion;
            }

            if (frontMotion != 0f)
            {
                // The acceleration looks wrong when combined with rotation.
                vTotalImpulse += LinearThrust * vFront * frontMotion / 256f;

                /*
                 * Move nose down when accelerating and vice versa.
                 */
                vTotalAngular += vRight * (-frontMotion / 256f * NoseDownWhileAcceleration);
            }

            if (upMotion != 0f)
            {
                vTotalImpulse += LinearThrust * vUp * upMotion / 256f;
            }

            if (turnMotion != 0f)
            {
                float fullThresh = 0.5f;
                float
                    damp = 1f; //(Single.Clamp(vTargetAngularVelocity.LengthSquared(), 0.1f, fullThresh) / fullThresh);
                turnMotion *= damp;

                /*
                 * gently lean to the right iof turning right.
                 */
                vTotalAngular += vFront * (turnMotion / 256f * WingsDownWhileTurning);

                /*
                 * And finally turn
                 */
                vTotalAngular += new Vector3(0f, AngularThrust * -turnMotion / 256f, 0f);
                vTotalImpulse += vTotalImpulse * (-0.3f * Single.Abs(turnMotion / 256f));
            }
        }

        /*
         * Now apply a damping on velocity, i.e. computing linear and angular impulses
         * proportional to the velocity
         */
        // vTotalImpulse += vTargetVelocity * -0.8f;
        // vTotalAngular += vTargetAngularVelocity * -0.8f;

        /*
         * Also, try to rotate me back to horizontal plane.
         * Look, where my local up vector points and find the shortes rotation to get it back up.
         * Do that by adding some angular impulse, making it swing in the end.
         */

        /*
         * We have two vectors, the perfect up vector and the real up vector.
         * If the real up vector only differs in minimal fractions from the perfect
         * one, we ignore the difference.
         *
         * If there is a larger deviation, we apply an angular impulse. The axis for
         * this impulse is the cross product of both of the up vectors.
         */
        Vector3 vSpinTopAxis = Vector3.Cross(vUp, new Vector3(0f, 1f, 0f));
        if (vSpinTopAxis.Length() > 0.01f)
        {
            vTotalAngular += 10f * vSpinTopAxis;
        }


        /*
         * Finally, clip the height with the ground.
         * To increase environment interaction, try to tilt the ship accordingly.
         */
        if (vTargetPos.Y < heightAtTarget)
        {
            /*
             * Read the height at our front, or kind of at the front.
             * Give us an impulse accordingly.
             */
            float heightAtFront = I.Get<engine.world.MetaGen>().Loader.GetNavigationHeightAt(
                vTargetPos + 2f * vFront);
            if (heightAtFront > heightAtTarget)
            {
                //heightAtTarget = heightAtFront;
                vTotalAngular += vRight * 10f * (heightAtFront - heightAtTarget);
            }

            vTargetPos.Y = heightAtTarget;
            lock (_engine.Simulation)
            {
                _prefTarget.Pose.Position = vTargetPos;
            }

            vTotalImpulse += new Vector3(0f, 10f, 0f);
        }

        var mass = 500f;
        if (vTotalImpulse.Length() > mass)
        {
            Trace($"Too fast: {vTotalImpulse.Length()}.");
        }

        if (vTotalAngular.Length() > mass)
        {
            Trace($"Too fast: {vTotalAngular.Length()}.");
        }


        /*
         * TXWTODO: Workaround to limit top speed.
         */
        if (vTargetVelocity.Length() > MaxLinearVelocity)
        {
            float vel = vTargetVelocity.Length();
            vTotalImpulse += -(vTargetVelocity * (vel - MaxLinearVelocity) / vel) / dt;
        }

        if (vTargetAngularVelocity.Length() > MaxAngularVelocity)
        {
            float avel = vTargetAngularVelocity.Length();
            vTotalAngular += -(vTargetAngularVelocity * (avel - MaxAngularVelocity) / avel) / dt;
        }

        Vector3 vNewTargetVelocity;
        Quaternion qTargetOrientation;
        lock (_engine.Simulation)
        {
            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massShip, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massShip);
            vNewTargetVelocity = _prefTarget.Velocity.Linear;
            qTargetOrientation = _prefTarget.Pose.Orientation;
        }

        /*
         * Set current velocity.
         */
        _eTarget.Set(new engine.joyce.components.Motion(vNewTargetVelocity));

        {
            var gameState = I.Get<GameState>();
            gameState.PlayerPosition = vTargetPos;
            gameState.PlayerOrientation = qTargetOrientation;
        }

    }


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;
        I.Get<engine.news.InputEventPipeline>().RemoveInputPart(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();

        _prefTarget = _eTarget.Get<engine.physics.components.Body>().Reference;

        I.Get<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }


    public void InputPartOnInputEvent(engine.news.Event ev)
    {
    }


    public WASDPhysics(
        in DefaultEcs.Entity eTarget,
        in float massShip)
    {
        _eTarget = eTarget;
        _massShip = massShip;
    }
}