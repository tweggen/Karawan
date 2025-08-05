using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using BepuPhysics.Collidables;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

/**
 * Walk is a kinematic physical object, so this one controls the position
 * as opposed to controlling the velocity or giving impulse to a dynamic
 * physical object.
 */
public class WalkController : AController, IInputPart
{
    public static float MY_Z_ORDER = 25f;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>()
    };


    private DefaultEcs.Entity _eTarget;

    public DefaultEcs.Entity Target
    {
        get => _eTarget;
        set => _eTarget = value;
    }


    private float _massTarget;

    public float MassTarget
    {
        get => _massTarget;
        set => _massTarget = value;
    }


    public Vector3 StartPosition { get; set; }
    public Quaternion StartOrientation { get; set; }
    
    public required uint CameraMask { get; set; }
    
    private DefaultEcs.Entity _eCamera = default;

    enum CharacterAnimState
    {
        Unset,
        Idle,
        Walking,
        Running,
        Jumping
    }
    
    
    /**
     * The vertical impulse we have. Increased by gravitation, if not on ground.
     */
    private float _verticalImpulse = 0f;


    enum JumpState
    {
        Grounded,
        Starting,
        InJump,
    }

    private JumpState _jumpState = JumpState.Grounded;
    
    
    private CharacterAnimState _characterAnimState = CharacterAnimState.Idle;
    
    private void _onCameraEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_eCamera != entity)
            {
                _eCamera = entity;
                isChanged = true;
            }
        }
    }
    
    public CharacterModelDescription CharacterModelDescription { get; set; }

    private bool _jumpTriggered = false;
    private bool _isRunPressed = false;
    
    
    protected override void OnLogicalFrame(object sender, float dt)
    {
        if (_engine.State != Engine.EngineState.Running) return;

        Vector3 vOrgTargetPos;
        Vector3 vOrgTargetVelocity;
        Quaternion qOrgTargetOrientation;

        Vector3 vNewTargetPos = Vector3.Zero;
        Vector3 vNewTargetVelocity = Vector3.Zero;
        Quaternion qNewTargetOrientation = Quaternion.Identity;
        
        /*
         * Read either from the object or take the initial position.
         */
        if (!_eTarget.Has<engine.joyce.components.Transform3>())
        {
            vOrgTargetPos = StartPosition;
            qOrgTargetOrientation = StartOrientation;
        }
        else
        {
            ref var cTransform3 = ref _eTarget.Get<Transform3>();
            vOrgTargetPos = cTransform3.Position;
            qOrgTargetOrientation = cTransform3.Rotation;
        }
        
        CharacterAnimState newAnimState = _characterAnimState;

        var vuFront = -Vector3.UnitZ;
        var vuUp = Vector3.UnitY;
        var vuRight = Vector3.UnitX;

        /*
         * In a perfect world, front and up are derived from the camera.
         * If we have a camera, load the camera orientation.
         */
        Quaternion qCameraOrientation = qOrgTargetOrientation;
        if (_eCamera != default && _eCamera.IsAlive && _eCamera.IsEnabled())
        {
            if (_eCamera.Has<Transform3ToWorld>())
            {
                ref var cTransform = ref _eCamera.Get<Transform3ToWorld>();
                vuRight = new Vector3(cTransform.Matrix.M11, cTransform.Matrix.M12, cTransform.Matrix.M13);
                vuRight.Y = 0f;
                
                /*
                 * Emergency workaround.
                 */
                if (vuRight.LengthSquared() == 0f)
                {
                    vuRight = Vector3.UnitX;
                }

                vuRight = Vector3.Normalize(vuRight);
                vuUp = Vector3.UnitY;
                vuFront = -Vector3.Cross(vuRight, vuUp);
            }
        } else
        if (_eTarget.Has<engine.joyce.components.Transform3ToParent>())
        {
            /*
             * First read target position/orientation
             */
            var cToParent = _eTarget.Get<engine.joyce.components.Transform3ToParent>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            vuFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            vuUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            vuRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);
        }

        /*
         * Keep player in bounds.
         */
        if (!MetaGen.AABB.Contains(vOrgTargetPos))
        {
            lock (_engine.Simulation)
            {
                _eTarget.Set(new Transform3(true, CameraMask, StartOrientation, StartPosition));
            }
            _eTarget.Set(new engine.joyce.components.Motion(Vector3.Zero));
            return;
        }

        I.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);

        var frontMotion = controllerState.FrontMotion;
        var upMotion = controllerState.UpMotion;
        var rightMotion = controllerState.RightMotion;

        bool haveVelocity = false;

        Vector3 vuWalkDirection = Vector3.Zero;
        /*
         * Set the movement velocity according to the inputs.
         */
        if (frontMotion > 0.2f)
        {
            vuWalkDirection += -Vector3.UnitZ;
            haveVelocity = true;
        }
        else if (frontMotion < -0.2f)
        {
            vuWalkDirection += Vector3.UnitZ;
            haveVelocity = true;
        }

        if (rightMotion > 0.2f)
        {
            vuWalkDirection += Vector3.UnitX;
            haveVelocity = true;
        }
        else if (rightMotion < -0.2f)
        {
            vuWalkDirection += -Vector3.UnitX;
            haveVelocity = true;
        }

        Quaternion qWalkFront; 

        if (haveVelocity)
        {
            vuWalkDirection = Vector3.Normalize(vuWalkDirection);
            
            /*
             * Walking speed in km/h
             */
            float speed;
            if (_isRunPressed)
            {
                speed = 15f;
            }
            else
            {
                speed = 8f;
            }
            vNewTargetVelocity += (-vuWalkDirection.Z * vuFront + vuWalkDirection.X * vuRight) * (speed / 3.6f);
            Vector3 vuWalkFront = Vector3.Normalize(vNewTargetVelocity);
            qWalkFront = engine.geom.Camera.CreateQuaternionFromPlaneFront(vuWalkFront);

            if (_isRunPressed)
            {
                newAnimState = CharacterAnimState.Running;
            }
            else
            {
                newAnimState = CharacterAnimState.Walking;
            }
        }
        else
        {
            vNewTargetVelocity += Vector3.Zero;
            qWalkFront = qOrgTargetOrientation;
            newAnimState = CharacterAnimState.Idle;
        }

        vNewTargetPos = vOrgTargetPos + vNewTargetVelocity * 1f / 60f;
        
        if (_jumpTriggered)
        {
            switch (_jumpState)
            {
                case JumpState.Grounded:
                    _jumpState = JumpState.Starting;
                    
                    /*
                     * Add an impulse of a sudden acceleration up of 10m/s.
                     */
                    _verticalImpulse += 6f;
                    break;
            }
            _jumpTriggered = false;
        }

        bool forceFrameZero = false;
        switch (_jumpState)
        {
            case JumpState.Grounded:
                break;
            default:
                newAnimState = CharacterAnimState.Jumping;
                forceFrameZero = true;
                break;
        }
        
        if (forceFrameZero || newAnimState != _characterAnimState)
        {
            if (CharacterModelDescription != null && CharacterModelDescription.Model != null &&
                CharacterModelDescription.EntityAnimations.IsAlive)
            {
                var jModel = CharacterModelDescription.Model;

                string strAnimation;
                switch (newAnimState)
                {
                    default:
                    case CharacterAnimState.Idle:
                        strAnimation = CharacterModelDescription.IdleAnimName;
                        break;
                    case CharacterAnimState.Walking:
                        strAnimation = CharacterModelDescription.WalkAnimName;
                        break;
                    case CharacterAnimState.Running:
                        strAnimation = CharacterModelDescription.RunAnimName;
                        break;
                    case CharacterAnimState.Jumping:
                        strAnimation = CharacterModelDescription.JumpAnimName;
                        break;
                }

                var mapAnimations = jModel.MapAnimations;
                if (mapAnimations != null && mapAnimations.Count > 0)
                {
                    if (mapAnimations.TryGetValue(
                            strAnimation, out var animation))
                    {

                        CharacterModelDescription.EntityAnimations.Set(new AnimationState
                        {
                            ModelAnimation = animation,
                            ModelAnimationFrame = 0
                        });
                        Trace($"Setting up animation {animation.Name}");
                    }
                    else
                    {
                        Trace($"Test animation {strAnimation} not found.");
                    }

                    _characterAnimState = newAnimState;
                }
            }
        }
        
        /*
         * First clip the height to world ground. Anything covered by physics must be above.
         */
        float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetWalkingHeightAt(vNewTargetPos);

        {
            if (vNewTargetPos.Y < heightAtTarget)
            {
                /*
                 * Are we below ground?
                 */
                // TXWTODO: Emit ground hit if I was above                
                vNewTargetPos.Y = heightAtTarget;
            }
        }

        bool isOnGround = false;
        
        /*
         * Use raycast in direction of motion to find how far we may go / fall.
         */
        lock (_engine.Simulation)
        {
            /*
             * First raycast old position in direction of new front motion
             */
            var aPhysics = I.Get<engine.physics.API>();
            float v2 = vNewTargetVelocity.LengthSquared(); 
            if (v2 > 0.01f)
            {
                Vector3 vuPhysicsFront;
                float v = Single.Sqrt(v2);

                vuPhysicsFront = vNewTargetVelocity / v;
                
                /*
                 * We look into physics front direction for only one physics frame, there's no need for more.
                 * Plus we use a hardcoded radius of 30cm.
                 * And we will collide with anything.
                 */
                float closestCollision = Single.MaxValue;
                CollidableReference? closestCollidable = null;
                CollisionProperties? collisionProperties = null;
                Vector3 vCollision;
                // TXWTODO: Workaround to raycast in eye line.
                aPhysics.RayCastSync(vNewTargetPos + 1.7f*Vector3.UnitY, vuPhysicsFront * 1f/60f, v,
                    (CollidableReference cRef, CollisionProperties props, float t, Vector3 vThisCollision) =>
                    {
                        bool isRelevant = false;
                        bool isMe = false;

                        if (props != null && props.Entity == _eTarget)
                        {
                            isMe = true;
                        }
                        
                        if (!isMe)
                        {
                            if (props != null)
                            {
                                // Trace($"Walk against {props.Name} {props.Entity}");
                                if (0 != (props.Flags & CollisionProperties.CollisionFlags.IsTangible))
                                {
                                    isRelevant = true;
                                }
                            }
                            else
                            {
                                switch (cRef.Mobility)
                                {
                                    case CollidableMobility.Dynamic:
                                    case CollidableMobility.Kinematic:
                                    case CollidableMobility.Static:
                                        isRelevant = true;
                                        break;
                                }
                            }
                        }


                        if (isRelevant)
                        {
                            if (t < closestCollision)
                            {
                                closestCollision = t;
                                closestCollidable = cRef;
                                collisionProperties = props;
                                vCollision = vThisCollision;
                            }
                        }
                    });

                if (closestCollision < Single.MaxValue)
                {
                    // Trace($"Closest collision in {closestCollision} total {collisionDistance}");
                    if (closestCollision < 0.3f)
                    {
                        /*
                         * We would run into a collision, so refuse to walk too far.
                         */

                        float movement = closestCollision - 0.3f;
                        vNewTargetVelocity = Vector3.Zero;
                        vNewTargetPos += vuPhysicsFront * movement;
                        // vNewTargetVelocity = vuPhysicsFront * movement;
                    }
                }
            }
            
            /*
             * Now look how far we could fall down. Again, use our head as a starting point.
             */
            {
                /*
                 * Look down 10m/s / 60frames/s, that should do for most falling.
                 */
                float closestCollision = Single.MaxValue;
                CollidableReference? closestCollidable = null;
                CollisionProperties? collisionProperties = null;
                Vector3 vCollision;
                // TXWTODO: Workaround to raycast in eye line.
                aPhysics.RayCastSync(vNewTargetPos + 1.7f*Vector3.UnitY, -Vector3.UnitY, 1.7f + 10f * 1f/60f,
                    (CollidableReference cRef, CollisionProperties props, float t, Vector3 vThisCollision) =>
                    {
                        bool isRelevant = false;
                        bool isMe = false;

                        if (props != null && props.Entity == _eTarget)
                        {
                            isMe = true;
                        }
                        
                        if (!isMe)
                        {
                            if (props != null)
                            {
                                if (0 != (props.Flags & CollisionProperties.CollisionFlags.IsTangible))
                                {
                                    isRelevant = true;
                                }
                            }
                            else
                            {
                                switch (cRef.Mobility)
                                {
                                    case CollidableMobility.Dynamic:
                                    case CollidableMobility.Kinematic:
                                    case CollidableMobility.Static:
                                        isRelevant = true;
                                        break;
                                }
                            }
                        }

                        if (isRelevant)
                        {
                            if (t < closestCollision)
                            {
                                closestCollision = t;
                                closestCollidable = cRef;
                                collisionProperties = props;
                                vCollision = vThisCollision;
                            }
                        }
                    });

                if (closestCollision < Single.MaxValue)
                {
                    if (closestCollision <= 1.7f)
                    {
                        /*
                         * We have our feet below ground, adjust, upwards. Still, we are on the ground.
                         * adjust, very fast.
                         */
                        isOnGround = true;
                        vNewTargetPos.Y += 1.7f - closestCollision;
                    }
                    else
                    {
                        if (closestCollision <= 2f)
                        {
                            if (_jumpState != JumpState.Grounded)
                            {
                                /*
                                 * If we are about to trigger a jump, we would not glue ourselves to the ground.
                                 */
                                isOnGround = false;
                            }
                            else
                            {
                                /*
                                 * Well, this is just stepping down, still we are on the ground, adjust, downwards,
                                 * gradually.
                                 */
                                isOnGround = true;
                                vNewTargetPos.Y += Single.Max(-10f * 1f / 60f, 1.7f - closestCollision);
                            }
                        }
                        else
                        {
                            /*
                             * Nothing below us.
                             */
                            isOnGround = false;
                        }
                    }
                }


                if (isOnGround)
                {
                    switch (_jumpState)
                    {
                        case JumpState.Starting:
                            _jumpState = JumpState.InJump;
                            break;
                        case JumpState.InJump:
                            _verticalImpulse = 0f;
                            _jumpState = JumpState.Grounded;
                            break;
                    }
                }
                else
                {
                    /*
                     * decrease the vertical velocity.
                     */
                    _verticalImpulse -= 10f * 1f / 60f;
                }

                /*
                 * no floor below my feet, start/continue to fall.
                 * Note, that this height (at minimum 30cm) should be above of what we
                 * would fall in this frame. check: after one second we would have
                 * 10m/s, so we would be below 10/60m/s == 1/6 m/s == 16cm / sec in the first frame.
                 * Matter of fact it is much less due to the acceleration curve (x^2 curve).
                 * So we safely can have the character fall downward.
                 */
                vNewTargetPos.Y += _verticalImpulse * 1f / 60f;
                
            }
        }


        // TXWTODO: Integrate acceleration to velocity to position only at this point.

        var qFinalWalkFront = Quaternion.Slerp(qOrgTargetOrientation, qWalkFront, 0.2f);
        I.Get<TransformApi>().SetTransforms(_eTarget, true, CameraMask, qFinalWalkFront, vNewTargetPos);
        _eTarget.Set(new engine.joyce.components.Motion(vNewTargetVelocity));
        
        {
            var gameState = M<AutoSave>().GameState;
            gameState.PlayerPosition = new(vNewTargetPos);
            gameState.PlayerOrientation = new(qFinalWalkFront);
            gameState.PlayerEntity = 1;
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        switch (ev.Type)
        {
            case Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<jump>":
                        _jumpTriggered = true;
                        ev.IsHandled = true;
                        break;
                    case "<run>":
                        _isRunPressed = true;
                        ev.IsHandled = true;
                        break;
                }

                break;
            case Event.INPUT_BUTTON_RELEASED:
                switch (ev.Code)
                {
                    case "<run>":
                        _isRunPressed = false;
                        ev.IsHandled = true;
                        break;
                }
                break;
        }
    }

    
    protected override void OnModuleDeactivate()
    {
        I.Get<InputEventPipeline>().RemoveInputPart(this);

        _engine.Camera.RemoveOnChange(_onCameraEntityChanged);
    }


    protected override void OnModuleActivate()
    {
        Debug.Assert(_eTarget != default);
        Debug.Assert(_massTarget != 0f);
        
        _characterAnimState = CharacterAnimState.Unset;
        
        _eCamera = _engine.Camera.Value;
        _engine.Camera.AddNowOnChange(_onCameraEntityChanged);
        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}