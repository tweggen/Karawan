using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using engine.physics;
using SkiaSharp;
using static engine.Logger;

namespace builtin.controllers
{
    public class FollowCameraController
    {
        engine.Engine _engine;
        DefaultEcs.Entity _eTarget;
        DefaultEcs.Entity _eCarrot;
        engine.joyce.TransformApi _aTransform;
        private Vector3 _vPreviousCameraPos;

        Quaternion _qLastPerfectCameraRotation;
        Vector2 _vMouseOffset;
        float _lastMouseMove = 0f;
        private bool _firstFrame = true;
        private bool _isInputEnabled = true;
        
        private float _previousZoomDistance = 33f;

        private float ORIENTATION_SLERP_AMOUNT = 0.07f;
        private float ZOOM_SLERP_AMOUNT = 0.05f;
        private float MOUSE_RELATIVE_AMOUNT = 0.03f;
        private float MOUSE_RETURN_SLERP = 0.98f;
        private float MOUSE_INACTIVE_BEFORE_RETURN_TIMEOUT = 1.6f;

        private long _frame = 0;

        public float CameraRadius { get; set; } = 0.5f;
        public float CameraMass { get; set; } = 0.5f;

        public float CameraDistance { get; set; } = 10.0f;
        public float CameraMinDistance { get; set; } = 2.0f;

        public string CameraPhysicsName { get; set; } = "CameraPhysics";
        
        private BodyReference _prefCameraBall;
        private BepuPhysics.Collidables.Sphere _pbodyCameraSphere;
        private BepuPhysics.Collidables.TypedIndex _pshapeCameraSphere;
        private BodyHandle _phandleCameraSphere;
        private ConstraintHandle _chandleCameraServo;
        private BodyInertia _pinertiaCameraSphere;
        private SpringSettings _cameraSpringSettings;
        private ServoSettings _cameraServoSettings;
        private BodyReference _prefPlayer;

        private void _buildPhysics()
        {

            Vector3 posShip = _prefPlayer.Pose.Position;
            Quaternion rotShip = _prefPlayer.Pose.Orientation;

            _cameraSpringSettings = new(5, 2);
            _cameraServoSettings = ServoSettings.Default;
            
            _pbodyCameraSphere = new(CameraRadius);
            _pinertiaCameraSphere = _pbodyCameraSphere.ComputeInertia(CameraMass);
            lock (_engine.Simulation)
            {
                _pshapeCameraSphere = _engine.Simulation.Shapes.Add(_pbodyCameraSphere);
                _phandleCameraSphere = _engine.Simulation.Bodies.Add(
                    BodyDescription.CreateDynamic(
                        new RigidPose(posShip, rotShip),
                        _pinertiaCameraSphere,
                        new CollidableDescription(
                            _pshapeCameraSphere,
                            0.1f
                        ),
                        new BodyActivityDescription(0.01f)
                    )
                );
                _chandleCameraServo = _engine.Simulation.Solver.Add(_phandleCameraSphere,
                    new OneBodyLinearServo()
                    {
                        SpringSettings = _cameraSpringSettings,
                        ServoSettings = _cameraServoSettings,
                        Target = Vector3.Zero,
                        LocalOffset = Vector3.Zero
                    });
                _prefCameraBall = _engine.Simulation.Bodies.GetBodyReference(_phandleCameraSphere);
                
                _eTarget.Set(new engine.physics.components.Body(
                    _prefCameraBall,
                    new engine.physics.CollisionProperties()
                    {
                        Entity = _eTarget,
                        Flags =
                            CollisionProperties.CollisionFlags.IsTangible
                            | CollisionProperties.CollisionFlags.IsDetectable,
                        Name = CameraPhysicsName
                    }));
            }

        }


        private void _destroyPhysics()
        {
            
        }

        private void _computePlainCameraPos(
            float dt,
            out Vector3 vPerfectCameraPos,
            out Vector3 vPerfectCameraDirection,
            out Vector3 vPerfectCameraFront,
            out Vector3 vPerfectCameraRight,
            out Quaternion qPerfectCameraOrientation)
        {
            var cToParent = _eCarrot.Get<engine.joyce.components.Transform3ToWorld>();
            var cCarrotTransform3 = _eCarrot.Get<engine.joyce.components.Transform3>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            vPerfectCameraRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);
            
            var vCarrotPos = cToParent.Matrix.Translation;

            float zoomDistance;
            if (_isInputEnabled)
            {
                engine.I.Get<builtin.controllers.InputController>()
                    .GetControllerState(out var controllerState);

                /*
                 * Compute a distance from the zoom state.
                 * TXWTODO: Remove the magic numbers.
                 */
                var zoomFactor = controllerState.ZoomState < 0
                    ? controllerState.ZoomState
                    : controllerState.ZoomState * 2f;
                zoomDistance = (36f - (float)zoomFactor) / 2f;
                zoomDistance = (1f - ZOOM_SLERP_AMOUNT) * _previousZoomDistance + zoomDistance * ZOOM_SLERP_AMOUNT;
                _previousZoomDistance = zoomDistance;
            }
            else
            {
                zoomDistance = _previousZoomDistance;
            }

            vPerfectCameraFront = vFront;
            vPerfectCameraDirection = zoomDistance * vPerfectCameraFront - zoomDistance/4f * vUp;
  
            vPerfectCameraPos = vCarrotPos - vPerfectCameraDirection;
            if (!_firstFrame)
            {
                var vCameraVelocity = (vPerfectCameraPos - _vPreviousCameraPos) / dt;
                _eTarget.Set(new engine.joyce.components.Motion(vCameraVelocity));
                _vPreviousCameraPos = vPerfectCameraPos;
            }

            var qRotation = Quaternion.Slerp(_qLastPerfectCameraRotation, cCarrotTransform3.Rotation, ORIENTATION_SLERP_AMOUNT);
            _qLastPerfectCameraRotation = qRotation;
            qPerfectCameraOrientation = qRotation;
        }
        
        
        private void _onLogicalFrame(object sender, float dt)
        {
            ++_frame;
            if (_frame >= 600)
            {
                ZOOM_SLERP_AMOUNT = 0.1f;
            }
            
            if( !_eCarrot.Has<engine.joyce.components.Transform3ToWorld>()
                || !_eCarrot.Has<engine.joyce.components.Transform3>())
            {
                return;
            }

            _computePlainCameraPos(dt,
                out var vPerfectCameraPos, 
                out var vPerfectCameraDirection, 
                out var vPerfectCameraFront, 
                out var vPerfectCameraRight,
                out var qPerfectCameraOrientation);
            
            /*
             * We allow the user to move the cam.
             */
            Vector2 vMouseMove;
            if (_isInputEnabled)
            {
                engine.I.Get<builtin.controllers.InputController>().GetMouseMove(out vMouseMove);
            }
            else
            {
                vMouseMove = Vector2.Zero;
            }

            /*
             * Some up the relative mouse movement.
             */
            _vMouseOffset += vMouseMove * MOUSE_RELATIVE_AMOUNT;
            if( vMouseMove.X == 0f && vMouseMove.Y == 0f )
            {
                _lastMouseMove += dt;
            }
            else
            {
                _lastMouseMove = 0f;
            }


            var angleX = -_vMouseOffset.Y * (float)Math.PI / 180f;
            var angleY = -_vMouseOffset.X * (float)Math.PI / 180f;
            var vUserCameraFront = 
                vPerfectCameraFront * (float)Math.Cos(-angleY)
                + vPerfectCameraRight * (float)Math.Sin(-angleY);

            /*
             * Apply relative mouse movement
             */
            var rotUp = Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), angleX);
            var rotRight = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), angleY);
            qPerfectCameraOrientation *= rotRight;
            qPerfectCameraOrientation *= rotUp;
            //_aTransform.SetTransform(_eTarget, qPerfectCameraOrientation, vPerfectCameraPos );

            /*
             * Now we have computed the position we want to target the camera object to.
             */
            lock (_engine.Simulation)
            {
                _prefCameraBall.Pose.Orientation = qPerfectCameraOrientation;
                _engine.Simulation.Solver.ApplyDescription(
                    _chandleCameraServo,
                    new OneBodyLinearServo()
                    {
                        LocalOffset = Vector3.Zero,
                        Target = vPerfectCameraPos,
                        SpringSettings = _cameraSpringSettings,
                        ServoSettings = _cameraServoSettings
                    });
            }

            /*
             * ramp down mouse offset after 1.5s of inactivity.
             */
            if( _lastMouseMove > MOUSE_INACTIVE_BEFORE_RETURN_TIMEOUT )
            {
                _vMouseOffset *= MOUSE_RETURN_SLERP;
            }
            
            _firstFrame = false;
        }

        
        public void ForcePreviousZoomDistance(float dist)
        {
            _previousZoomDistance = dist;
        }


        public void EnableInput(bool isInputEnabled)
        {
            _isInputEnabled = isInputEnabled;
        }
        
        
        public void DeactivateController()
        {
            _destroyPhysics();
            _engine.OnLogicalFrame -= _onLogicalFrame;
        }

        
        public void ActivateController()
        {
            _buildPhysics();
            _engine.OnLogicalFrame += _onLogicalFrame;
        }

        
        public FollowCameraController(engine.Engine engine0, DefaultEcs.Entity eTarget, DefaultEcs.Entity eCarrot) 
        {
            _engine = engine0;
            _eTarget = eTarget;
            _eCarrot = eCarrot;
            if (!_eCarrot.Has<engine.physics.components.Body>())
            {
                ErrorThrow($"Entity {eCarrot} does not have physics attached.", m => new InvalidOperationException(m));
                return;
            }
            _prefPlayer = _eCarrot.Get<engine.physics.components.Body>().Reference;

            if (_eTarget.Has<engine.physics.components.Body>())
            {
                ErrorThrow($"Entity {eTarget} already has physics attached.", m => new InvalidOperationException(m));
            }
           _aTransform = engine.I.Get<engine.joyce.TransformApi>();
        }
    }
}
