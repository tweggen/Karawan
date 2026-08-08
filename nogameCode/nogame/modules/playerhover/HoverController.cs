using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.Numerics;
using engine;
using engine.draw;
using engine.geom;
using engine.news;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

internal class HoverController : AController
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>()
    };
    
    
    private DefaultEcs.Entity _eTarget;
    public DefaultEcs.Entity Target { get => _eTarget; set => _eTarget = value; }


    private float _massTarget;
    public float MassTarget { get => _massTarget; set => _massTarget = value; }

    private BepuPhysics.BodyReference _prefTarget;

    public float LinearThrust { get; set; } = 15f;
    public float AngularThrust { get; set; } = 40.0f;

    public float MaxLinearVelocity { get; set; } = 220f;
    public float MaxAngularVelocity { get; set; } = 0.8f;
    public float LevelUpThrust { get; set; } = 48f;
    public float LevelDownThrust { get; set; } = 16f;
    public float NoseDownWhileAcceleration { get; set; } = 0.5f;
    public float WingsDownWhileTurning { get; set; } = 1.5f;

    public float InputTurnThreshold { get; set; } = 150f;
    public float FirstInputTurnThreshold { get; set; } = 4f;

    public float AngularDamping { get; set; } = 0.999f;
    public float LinearDamping { get; set; } = 0.6f;


    private float _lastTurnMotion = 0f;
    private bool _traceControllers;


    /**
     * How far past the normal limits the body may get before the emergency clamp intervenes.
     * Generous on purpose: the proportional limiters own the normal range, and this must not
     * interfere with them or with any deliberate overspeed the game arranges.
     */
    public float EmergencyVelocityFactor { get; set; } = 20f;


    /**
     * Bound the body's velocity in place. Caller must hold the simulation lock.
     *
     * Rescaling rather than correcting: a correction is proportional and can overshoot, which is
     * exactly how a 0.8 rad/s limit ended up being exceeded by twenty-one orders of magnitude.
     * Setting the magnitude directly has no gain and therefore no feedback loop.
     *
     * Every comparison is negated (`!(x <= limit)`) so that NaN and Infinity take the clamp branch
     * instead of slipping past it, and each axis is checked component-wise BEFORE taking a length,
     * because squaring a component near 1E+21 overflows a float and turns the length into Infinity
     * while the components still look finite.
     */
    private void _clampBodyVelocity()
    {
        float maxLinear = MaxLinearVelocity * EmergencyVelocityFactor;
        float maxAngular = MaxAngularVelocity * EmergencyVelocityFactor;

        Vector3 vLinear = _prefTarget.Velocity.Linear;
        Vector3 vAngular = _prefTarget.Velocity.Angular;

        if (!_isWithin(vLinear, maxLinear))
        {
            Error($"Emergency clamp: linear velocity {vLinear} exceeds {maxLinear}; resetting to zero.");
            _prefTarget.Velocity.Linear = Vector3.Zero;
        }

        if (!_isWithin(vAngular, maxAngular))
        {
            Error($"Emergency clamp: angular velocity {vAngular} exceeds {maxAngular}; resetting to zero.");
            _prefTarget.Velocity.Angular = Vector3.Zero;
        }

        /*
         * The pose can already be non-finite by the time we get here, and unlike velocity it cannot
         * be recovered by scaling - there is no "slightly wrong" NaN position. Put the body back on
         * the last pose we were willing to persist, which is by construction a finite one.
         */
        Vector3 vPosition = _prefTarget.Pose.Position;
        Quaternion qOrientation = _prefTarget.Pose.Orientation;

        if (!_isFinite(vPosition) || !_isFinite(qOrientation))
        {
            var gameState = M<AutoSave>().GameState;
            Error($"Emergency clamp: pose position {vPosition} orientation {qOrientation} is not "
                  + $"finite; restoring the last persisted pose {gameState.PlayerPosition} "
                  + $"{gameState.PlayerOrientation}.");

            _prefTarget.Pose.Position = gameState.PlayerPosition;
            _prefTarget.Pose.Orientation = builtin.tools.SafeOrientation.Sanitize(
                gameState.PlayerOrientation, "HoverController._clampBodyVelocity");
            _prefTarget.Velocity.Linear = Vector3.Zero;
            _prefTarget.Velocity.Angular = Vector3.Zero;
        }
    }


    private static bool _isFinite(Vector3 v)
        => Single.IsFinite(v.X) && Single.IsFinite(v.Y) && Single.IsFinite(v.Z);


    private static bool _isFinite(Quaternion q)
        => Single.IsFinite(q.X) && Single.IsFinite(q.Y) && Single.IsFinite(q.Z) && Single.IsFinite(q.W);


    /**
     * Component-wise first, so a component large enough to overflow when squared is caught before
     * the length is ever computed.
     */
    private static bool _isWithin(Vector3 v, float limit)
    {
        if (!(Single.Abs(v.X) <= limit) || !(Single.Abs(v.Y) <= limit) || !(Single.Abs(v.Z) <= limit))
        {
            return false;
        }

        return v.Length() <= limit;
    }


    protected override void OnLogicalFrame(object sender, float dt)
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
        if (_engine.State == Engine.EngineState.Running)
        {
            I.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);

            /*
             * The front motion of the hover is the controller's bumps
             * plus wasd vertical plus touch device vertical.
             */
            var frontMotion = 0
                + controllerState.BumpersMotion
                + controllerState.WASDVert
                + controllerState.TouchLeftStickVert;
            if (_traceControllers && (_engine.FrameNumber & 0x7) == 0)
            {
                Trace(_dc,
                    $"BumpersMotion = {controllerState.BumpersMotion}, TouchLeftVert = {controllerState.TouchLeftStickVert}");
            }

            /*
             * The up motion is not supported today.
             */
            var upMotion = 0;
                // controllerState.AnalogLeftStickVert;
            float turnMotion;
            {
                // TXWTODO: Define constants for each of the sensitivities
                /*
                 * The left/right turn motion is
                 * left stick left right (touch or physical) + a/d keyboard.
                 */
                float inputTurnMotion = 0f
                    + controllerState.AnalogLeftStickHoriz
                    + controllerState.WASDHoriz
                    + controllerState.TouchLeftPushHoriz * 0.5f;
                 
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
                vTotalImpulse += 2f * LinearThrust * vUp * upMotion / 256f;
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

        /*
         * These two used to both say just "Too fast", so the log could not tell you WHICH of the
         * two had run away - and being plain Trace() they ignored the debug category convention.
         * They are the earliest warning that an input magnitude is reaching the physics wrong, so
         * they are worth being able to read.
         *
         * Negated comparisons deliberately: `!(x <= mass)` is true for NaN, `x > mass` is false.
         * If a value has already gone non-finite, that is exactly when you want to hear about it.
         */
        /*
         * Warning, not Trace(_dc, ...). #45 moved these behind the Dc.Input category, and the
         * consequence showed up immediately: a device log with a full angular runaway in it
         * contained no "Too fast" line at all, because that category was not enabled. An impulse
         * exceeding its sane bound is an abnormal condition, not routine chatter, and the whole
         * value of these two lines is being there when nobody thought to switch anything on.
         */
        var mass = 500f;
        if (!(vTotalImpulse.Length() <= mass))
        {
            Warning($"Too fast, LINEAR impulse {vTotalImpulse} (magnitude "
                    + $"{vTotalImpulse.Length()}, limit {mass}).");
        }

        if (!(vTotalAngular.Length() <= mass))
        {
            Warning($"Too fast, ANGULAR impulse {vTotalAngular} (magnitude "
                    + $"{vTotalAngular.Length()}, limit {mass}).");
        }


        /*
         * TXWTODO: Workaround to limit top speed.
         */
        /*
         * These are the speed limiters, and they were NaN-blind in the direction that matters:
         * `vel > Max` is false when vel is NaN, so the moment the body goes non-finite the limiter
         * stops engaging - permanently, for exactly the state it exists to contain. Same defect as
         * the camera's `l < 0.5f` guard.
         *
         * The negated form fires for NaN too. Correcting the velocity by a NaN would be pointless,
         * so a non-finite reading is reported and the impulse left alone rather than poisoned
         * further; the pose guard further down then refuses to persist the result.
         */
        float vel = vTargetVelocity.Length();
        if (!(vel <= MaxLinearVelocity))
        {
            if (Single.IsFinite(vel))
            {
                vTotalImpulse += -(vTargetVelocity * (vel - MaxLinearVelocity) / vel) / dt;
            }
            else
            {
                Error($"Player linear velocity {vTargetVelocity} has a length of {vel}; "
                      + $"cannot compute a correction. Emergency clamp will handle it.");
            }
        }

        float avel = vTargetAngularVelocity.Length();
        if (!(avel <= MaxAngularVelocity))
        {
            if (Single.IsFinite(avel))
            {
                vTotalAngular += -(vTargetAngularVelocity * (avel - MaxAngularVelocity) / avel) / dt;
            }
            else
            {
                /*
                 * Note the components can each be finite while the LENGTH is Infinity: observed
                 * <4.0E+21, -4.7E+21, 2.66E+20>, and squaring 4E+21 overflows a float (max 3.4E+38)
                 * long before the sum is taken. The old message said "is not finite" and pointed
                 * at the wrong thing.
                 */
                Error($"Player angular velocity {vTargetAngularVelocity} has a length of {avel}; "
                      + $"cannot compute a correction. Emergency clamp will handle it.");
            }
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
            // _clampBodyVelocity is called below; it assumes this lock is already held.
            /*
             * First apply all impulses, angular and linear.
             */
            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massTarget, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massTarget);

            /*
             * EMERGENCY CLAMP. Everything above computes a CORRECTION and hopes the body converges;
             * this bounds the state outright, and it is the difference between a bad second and a
             * dead session.
             *
             * Observed on device: angular velocity <4.0E+21, -4.7E+21, 2.66E+20> against a
             * MaxAngularVelocity of 0.8 - twenty-one orders of magnitude over, reached about half a
             * second after a touch. The proportional limiter cannot recover from that and in fact
             * made it worse: with the length overflowing to Infinity, its own correction term
             * (avel - Max)/avel is Infinity/Infinity = NaN, so the "limiter" was handing a NaN
             * impulse to the body. That is where the NaN orientation came from.
             *
             * So the emergency bound is deliberately NOT the proportional path. It rescales the
             * velocity in place, which cannot overshoot, cannot oscillate and needs no inertia
             * assumptions. It is set well above the normal limit so ordinary play never reaches it
             * - crossing it means a term upstream has misbehaved, hence Error, not Warning.
             *
             * This does not explain WHY the runaway starts. It stops it from being unrecoverable
             * while that is investigated.
             */
            _clampBodyVelocity();
            
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

            /*
             * NEVER persist a pose that is not finite. This is the step that turns a transient
             * physics excursion into a permanent, cross-launch failure.
             *
             * vTargetPos and qFinalTargetOrientation both derive from the body pose. If the body
             * has been driven to an extreme state, the pose can go non-finite, and writing it here
             * hands it to AutoSave. On the next launch HoverModule._setupPlayer feeds it straight
             * back to BepuPhysics.Bodies.Add, whose "Orientation should be initialized to a unit
             * length quaternion" assert aborts the process on Android - a boot loop that survives
             * reinstalling, because the bad value is in the save, not the code.
             *
             * builtin.tools.SafeOrientation now repairs it on the READ side, so the loop is broken
             * either way. This is the other half: keeping the last known-good pose is strictly
             * better than persisting a known-bad one, and it means a save can never be poisoned
             * in the first place.
             */
            bool isPoseUsable =
                Single.IsFinite(vTargetPos.X) && Single.IsFinite(vTargetPos.Y) && Single.IsFinite(vTargetPos.Z)
                && Single.IsFinite(qFinalTargetOrientation.X) && Single.IsFinite(qFinalTargetOrientation.Y)
                && Single.IsFinite(qFinalTargetOrientation.Z) && Single.IsFinite(qFinalTargetOrientation.W);

            if (isPoseUsable)
            {
                gameState.PlayerPosition = new(vTargetPos);
                gameState.PlayerOrientation = new(qFinalTargetOrientation);
                gameState.PlayerEntity = 0;
            }
            else
            {
                Error($"Refusing to persist a non-finite player pose: position {vTargetPos}, "
                      + $"orientation {qFinalTargetOrientation}. Keeping the previous saved pose. "
                      + $"The physics body is in a broken state - look for 'Too fast' above.");
            }
        }

    }


    protected override void OnModuleActivate()
    {
        Debug.Assert(_eTarget != default);
        Debug.Assert(_massTarget != 0f);

        _prefTarget = _eTarget.Get<engine.physics.components.Body>().Reference;
   }
}