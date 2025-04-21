using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.joyce.components;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

public class WalkController : AModule
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


    private BepuPhysics.BodyReference _prefTarget;
    private DefaultEcs.Entity _eCamera = default;

    
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
    
    
    private void _onLogicalFrame(object sender, float dt)
    {
        if (_engine.State != Engine.EngineState.Running) return;

        Vector3 vTargetPos;
        Vector3 vTargetVelocity;
        Quaternion qTargetOrientation;
        Vector3 vTargetAngularVelocity;
        Vector3 vTargetPosAdjust = Vector3.Zero;
        lock (_engine.Simulation)
        {
            vTargetPos = _prefTarget.Pose.Position;
            vTargetVelocity = _prefTarget.Velocity.Linear;
            vTargetAngularVelocity = _prefTarget.Velocity.Angular;
            qTargetOrientation = _prefTarget.Pose.Orientation;
            //Trace($"vTargetVelocity = {vTargetVelocity}, vTargetPos = {vTargetPos}");
        }

        var vuFront = -Vector3.UnitZ;
        var vuUp = Vector3.UnitY;
        var vuRight = Vector3.UnitX;

        /*
         * In a perfect world, front and up are derived from the camera.
         * If we have a camera, load the camera orientation.
         */
        Quaternion qCameraOrientation = qTargetOrientation;
        if (_eCamera != default && _eCamera.IsAlive && _eCamera.IsEnabled())
        {
            if (_eCamera.Has<engine.physics.components.Body>())
            {
                ref var cBody = ref _eCamera.Get<engine.physics.components.Body>();
                lock (_engine.Simulation)
                {
                    qCameraOrientation = cBody.Reference.Pose.Orientation;
                    qTargetOrientation = qCameraOrientation;
                }
            }

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
        if (!MetaGen.AABB.Contains(vTargetPos))
        {
            lock (_engine.Simulation)
            {
                _prefTarget.Pose.Orientation = Quaternion.Identity;
                _prefTarget.Velocity.Angular = Vector3.Zero;
                _prefTarget.Velocity.Linear = Vector3.Zero;
            }

            _eTarget.Set(new engine.joyce.components.Motion(_prefTarget.Velocity.Linear));

            return;
        }

        I.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);

        var frontMotion = controllerState.FrontMotion;
        var upMotion = controllerState.UpMotion;
        var rightMotion = controllerState.RightMotion;

        /*
         * Set the movement velocity according to the inputs.
         */
        if (frontMotion > 0.2f)
        {
            vTargetVelocity = vuFront * (8f / 3.6f);
        }
        else if (frontMotion < -0.2f)
        {
            vTargetVelocity = -vuFront * (5f / 3.6f);
        }
        else
        {
            vTargetVelocity = Vector3.Zero;
        }

        if (rightMotion > 0.2f)
        {
            vTargetVelocity = 0.8f * Vector3.Dot(vTargetVelocity, vuFront) * vuFront + vuRight * (2f / 3.6f);
        }
        else if (rightMotion < -0.2f)
        {
            vTargetVelocity = 0.8f * Vector3.Dot(vTargetVelocity, vuFront) * vuFront - vuRight * (2f / 3.6f);
        }

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
        
        lock (_engine.Simulation)
        {
            _prefTarget.Velocity.Linear = vTargetVelocity;
            _prefTarget.Velocity.Angular = vTargetAngularVelocity;
            _prefTarget.Pose.Position += vTargetPosAdjust;
            _prefTarget.Pose.Orientation = qTargetOrientation;
            _prefTarget.Awake = true;
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

        _prefTarget = _eTarget.Get<engine.physics.components.Body>().Reference;

        _engine.AddModule(this);
        
        _eCamera = _engine.Camera.Value;
        _engine.Camera.AddOnChange(_onCameraEntityChanged);
        _engine.OnLogicalFrame += _onLogicalFrame;
        
    }
}