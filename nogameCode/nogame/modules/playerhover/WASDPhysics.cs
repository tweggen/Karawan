using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
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

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>()
    };
    
    
    private DefaultEcs.Entity _eTarget;

    private BepuPhysics.BodyReference _prefTarget;

    private float _massShip;

    public float LinearThrust { get; set; } = 25f;
    public float AngularThrust { get; set; } = 50.0f;

    public float MaxLinearVelocity { get; set; } = 220f;
    public float MaxAngularVelocity { get; set; } = 0.8f;
    public float LevelUpThrust { get; set; } = 16f;
    public float LevelDownThrust { get; set; } = 16f;
    public float NoseDownWhileAcceleration { get; set; } = 0.5f;
    public float WingsDownWhileTurning { get; set; } = 1.5f;

    public float InputTurnThreshold { get; set; } = 150f;
    public float FirstInputTurnThreshold { get; set; } = 4f;

    public float AngularDamping { get; set; } = 0.999f;
    public float LinearDamping { get; set; } = 0.6f;


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
        Quaternion qTargetOrientation;
        Vector3 vTargetAngularVelocity;
        lock (_engine.Simulation)
        {
            vTargetPos = _prefTarget.Pose.Position;
            vTargetVelocity = _prefTarget.Velocity.Linear;
            vTargetAngularVelocity = _prefTarget.Velocity.Angular;
            qTargetOrientation = _prefTarget.Pose.Orientation;
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
        var cToParent = _eTarget.Get<engine.joyce.components.Transform3ToParent>();

        /*
         * We cheat a bit, reading the matrix for the direction matrix,
         * applying the position change to the transform parameters,
         * applying rotation directly to the transform parameters.
         */
        var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
        var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
        var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);

        float radiansTurnVehicle = 0f;

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

            /*
             * Apply turn motion.
             * This just changes the impulse vector rather than creating an angular impulse.
             * We still read the absolute value we needed to change (i.e. the force)
             * to do something like squeeking tires.
             */
            if (turnMotion != 0f)
            {
                /*
                 * gently lean to the right iof turning right.
                 */
                vTotalAngular += vFront * (turnMotion / 256f * WingsDownWhileTurning);
                
                /*
                 * Compute the angle per frame. TurnMotion ranges from -255 ... 255.
                 * We try to start with 120 degrees per second.
                 */
                float degreesPerSecond = 120f;
                
                /*
                 * We need to consider the direction depending on the effective speed of the vehicle
                 * in direction of its front
                 */
                float direction;
                if (Vector3.Dot(vFront with { Y = 0f }, vTargetVelocity with { Y=0f } ) > 0)
                {
                    direction = 1f;
                }
                else
                {
                    if (frontMotion >= -1)
                    {
                        direction = 1f;
                    }
                    else
                    {
                        direction = -1f;
                    }
                }
                radiansTurnVehicle += direction * ((float)turnMotion / 255f) * degreesPerSecond / 180f * Single.Pi * dt;
            }
        }

        /*
         * Now apply a damping on velocity, i.e. computing linear and angular impulses
         * proportional to the velocity
         */

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
        
        /*
         * Let the ship gradually slow down.
         */
        if (true) {
            vTotalImpulse -= LinearDamping * vTargetVelocity;
            vTotalAngular -= AngularDamping * vTargetAngularVelocity;
        }

        /*
         * Remove parts of the velocity that are not in the direction of the ship.
         */
        if(true) {
            Vector3 v3Off = vTargetVelocity - Vector3.Dot(vTargetVelocity, vFront) * vFront;
            vTotalImpulse -= 2f*v3Off;
        }
        
        Vector3 vFinalTargetVelocity;
        Quaternion qFinalTargetOrientation;
        lock (_engine.Simulation)
        {
            /*
             * First apply all impulses, angular and linear.
             */
            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massShip, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massShip);
            
            /*
             * Now manipulate the velocity according to the spec.
             * However, we need to read it back after applying the impulse (which also just applies it to the velocity).
             */
            vTargetVelocity = _prefTarget.Velocity.Linear;
            float a = radiansTurnVehicle;
            Vector3 vTargetNewVelocity = new(
                vTargetVelocity.X*Single.Cos(a) - vTargetVelocity.Z*Single.Sin(a),
                vTargetVelocity.Y,
                vTargetVelocity.Z*Single.Cos(a) + vTargetVelocity.X*Single.Sin(a)
            );
            Quaternion qTargetNewOrientation = Quaternion.Concatenate(qTargetOrientation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -a));

            _prefTarget.Velocity.Linear = vTargetNewVelocity;
            _prefTarget.Pose.Orientation = qTargetNewOrientation;

            vFinalTargetVelocity = vTargetNewVelocity;
            qFinalTargetOrientation = qTargetNewOrientation;
        }

        /*
         * Set current velocity.
         */
        _eTarget.Set(new engine.joyce.components.Motion(vFinalTargetVelocity));

        {
            var gameState = M<AutoSave>().GameState;
            gameState.PlayerPosition = vTargetPos;
            gameState.PlayerOrientation = qFinalTargetOrientation;
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