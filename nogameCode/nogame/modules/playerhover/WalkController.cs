using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.joyce.components;
using engine.news;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

/**
 * Walk is a kinematic physical object, so this one controls the position
 * as opposed to controlling the velocity or giving impulse to a dynamic
 * physical object.
 */
public class WalkController : AModule, IInputPart
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
    
    public uint CameraMask { get; set; }
    
    private DefaultEcs.Entity _eCamera = default;

    enum CharacterAnimState
    {
        Unset,
        Idle,
        Walking,
        Running
    }

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
    
    
    private void _onLogicalFrame(object sender, float dt)
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
            vNewTargetVelocity += (-vuWalkDirection.Z * vuFront + vuWalkDirection.X * vuRight) * (8f / 3.6f);
            Vector3 vuWalkFront = Vector3.Normalize(vNewTargetVelocity);
            qWalkFront = engine.geom.Camera.CreateQuaternionFromPlaneFront(vuWalkFront);
            newAnimState = CharacterAnimState.Walking;
        }
        else
        {
            vNewTargetVelocity += Vector3.Zero;
            qWalkFront = qOrgTargetOrientation;
            newAnimState = CharacterAnimState.Idle;
        }

        if (_jumpTriggered)
        {
            _jumpTriggered = false;
            vNewTargetVelocity += Vector3.UnitY;
        }
        
        if (newAnimState != _characterAnimState)
        {
            if (CharacterModelDescription != null && CharacterModelDescription.Model != null && CharacterModelDescription.EntityAnimations.IsAlive)
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
                        Trace($"Test animation {CharacterModelDescription.IdleAnimName} not found.");
                    }
                    
                    _characterAnimState = newAnimState;
                }
            }
        }

        #if false
        /*
         * Finally clip the height.
         *
         * If the player is above ground, let gravity do it's thing,
         * capping velocity to vFallMax.
         */
        float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetWalkingHeightAt(vTargetPos);

        {
            var properDeltaY = 0;
            var deltaY = vTargetPos.Y - (heightAtTarget + properDeltaY);
            const float threshDiff = 0.01f;

            if (vTargetPos.Y < heightAtTarget)
            {
                /*
                 * Are we below ground?
                 */
                // TXWTODO: Emit ground hit if I was above                
                vTargetPosAdjust.Y = heightAtTarget - vTargetPos.Y;
                vTargetVelocity.Y = 0;
            }
            else
            {
                /*
                 * Are we above ground? Then limit falling speed.
                 * If we are falling faster than 100km/h (20m/s), limit.
                 */
                if (vTargetVelocity.Y < -20f)
                {
                    vTargetVelocity.Y = -20f;
                }
            }

            // TXWTODO: Add jump.
        }
        #endif
        
        lock (_engine.Simulation)
        {
            _eTarget.Set(new Transform3(true, CameraMask, qWalkFront, vNewTargetPos));
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type == Event.INPUT_BUTTON_PRESSED)
        {
            switch (ev.Code)
            {
                case "<jump>":
                    _jumpTriggered = true;
                    break;
            }
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _engine.Camera.AddOnChange(_onCameraEntityChanged);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        Debug.Assert(_eTarget != default);
        Debug.Assert(_massTarget != 0f);
        
        base.ModuleActivate();

        _engine.AddModule(this);

        _characterAnimState = CharacterAnimState.Unset;
        
        _eCamera = _engine.Camera.Value;
        _engine.Camera.AddOnChange(_onCameraEntityChanged);
        _engine.OnLogicalFrame += _onLogicalFrame;
        
    }
}