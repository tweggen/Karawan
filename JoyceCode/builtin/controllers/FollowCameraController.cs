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
        
        private Vector3 _vPreviousCameraPosition;
        private Vector3 _vPreviousCameraOffset;
        private Quaternion _qLastPerfectCameraRotation;
        private Vector2 _vMouseOffset;
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

        
        /**
         * Compute the desired camera position.
         *
         * Note, the orientation is included just for historic reasons.
         *
         * The desired position is input to the physics system as a goal to reach.
         * However, buildings, object etc. might obstruct the path so we do not
         * reach it.
         */
        private void _computePlainCameraPos(
            in Matrix4x4 cToParentMatrix,
            out Vector3 vPerfectCameraPos,
            out Vector3 vPerfectCameraOffset)
        {
            var vCarrotPos = cToParentMatrix.Translation;


            
            /*
             * First compute the desired direction of the camera. The camera is position up
             * and backward with respect to the direction to the subject (the carrot).
             * 
             * If we have physics, we take the velocity of the carrot as direction.
             * 
             * If the velocity is too small, we do not change the direction.
             *
             * If we do not have physics, we compute it from the orientation of the target.
             *
             * If the direction has not been initialised before, we derive the direction from
             * the orientation of the target.
             *
             * The "right" vector is computed by assuming the up vector is the y axis.
             * The correct up vector is computed from the front and from the right vector.
             */
            
            
            bool haveFront = false;
            float l;
            Vector3 vFront = new(0f, 0f, -1f);
            Vector3 vUp;
            Vector3 vOrientationFront = new Vector3(-cToParentMatrix.M31, 0f, -cToParentMatrix.M33);
            {
                l = vOrientationFront.Length();
                if (l < 0.3f)
                {
                    vOrientationFront = new(0f, 0f, -1f);
                }
                else
                {
                    vOrientationFront /= l;
                }
            }

            bool isBackward = false;
            
            if (!haveFront && _prefPlayer.Exists)
            {
                var vVelocity = _prefPlayer.Velocity.Linear;
                Vector2 vVelocity2 = new(vVelocity.X, vVelocity.Z);
                float velocity = vVelocity2.Length();
                if (velocity > 3f / 3.6f)
                {
                    var vFront2 = vVelocity2 / velocity;
                    vFront = new Vector3(vFront2.X, 0f, vFront2.Y);
                    
                    /*
                     * Also check, if we are riding forward or backward
                     */
                    float dir = Vector3.Dot(vFront, vOrientationFront);
                    if (dir < 0) isBackward = true;

                    if (isBackward)
                    {
                        vFront = -vFront;
                    }
                    haveFront = true;
                }
            }

            if (!haveFront && _vPreviousCameraOffset != default)
            {
                Vector2 vVelocity2 = new(_vPreviousCameraOffset.X, _vPreviousCameraOffset.Z);
                l = vVelocity2.Length();
                if (l > 0.5)
                {
                    var vFront2 = vVelocity2 / l;
                    vFront = new Vector3(vFront2.X, 0f, vFront2.Y);
                    haveFront = true;
                }
            }

            if (!haveFront)
            {
                vFront = new Vector3(-cToParentMatrix.M31, 0f, -cToParentMatrix.M33);
                l = vFront.Length();
                if (l < 0.3f)
                {
                    vFront = new(0f, 0f, -1f);
                }
                else
                {
                    vFront /= vFront.Length();
                }
                haveFront = true;
            }

            if (!haveFront)
            {
                ErrorThrow($"No camera front information available.", m => new InvalidOperationException(m));
            }

            /*
             * Derive right from front and the Y vector, up is straight up,
             * real front then again from -(richt x up) 
             */
            vUp = new Vector3(0f, 1f, 0f);
            var vPerfectCameraRight = Vector3.Cross(vFront, vUp);
            var vPerfectCameraFront = -Vector3.Cross(vPerfectCameraRight, vUp);
            
            /*
             * Now we have a camera corrdinate system.
             * Front (i.e. -z) facing the carrot, up and right as we want it.
             *
             * Now let's move the camera back to where we want it to be.
             */
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
            vPerfectCameraOffset = zoomDistance * vPerfectCameraFront - zoomDistance/4f * vUp;
  
            vPerfectCameraPos = vCarrotPos - vPerfectCameraOffset;
            
        }


        /**
         * The real camera transformation is defined by the positon of the physical camera object
         * and a direction that we want to determine.
         */
        private void _computeCameraDirection(
            in Vector3 vRealCameraPosition,
            in Vector3 vCarrotPos,
            out Vector3 vRealCameraOffset,
            out Vector3 vRealCameraRight,
            out Vector3 vRealCameraUp,
            out Quaternion qRealCameraOrientation
        )
        {
            vRealCameraOffset = vCarrotPos -vRealCameraPosition;
            float l = vRealCameraOffset.Length();
            if (l < 0.5f)
            {
                vRealCameraOffset = _vPreviousCameraOffset;
                l = vRealCameraOffset.Length();
                if (l < 0.5f)
                {
                    vRealCameraOffset = new Vector3(0f, 0f, 1f);
                }
                else
                {
                    vRealCameraOffset /= l;
                }
            }
            else
            {
                vRealCameraOffset /= l;
            }
            
            /*
             * Derive
             * - right from front and the Y vector
             * - up from (right x front)
             */
            
            vRealCameraRight = Vector3.Cross(vRealCameraOffset, new Vector3(0f, 1f, 0f));
            vRealCameraUp = Vector3.Cross(vRealCameraRight, vRealCameraOffset);

            /*
             * Compute camera orientation from the coordinate system.
             */
            var qCarrotOrientation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateWorld(
                    Vector3.Zero,
                    vRealCameraOffset,
                    vRealCameraUp)
            );

            var qOrientation = Quaternion.Slerp(_qLastPerfectCameraRotation, qCarrotOrientation, ORIENTATION_SLERP_AMOUNT);
            _qLastPerfectCameraRotation = qOrientation;
            qRealCameraOrientation = qOrientation;
        }

        
        private void _computeCameraVelocity(float dt, Vector3 vRealCameraPos)
        {
            /*
             * Compute camera object velocity for audio effects etc.
             */
            if (!_firstFrame)
            {
                var vCameraVelocity = (vRealCameraPos - _vPreviousCameraPosition) / dt;
                _eTarget.Set(new engine.joyce.components.Motion(vCameraVelocity));
            }
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
            
            var cToParent = _eCarrot.Get<engine.joyce.components.Transform3ToWorld>();
            var vCarrotPos = cToParent.Matrix.Translation;

            _computePlainCameraPos(
                cToParent.Matrix,
                out var vPerfectCameraPos, 
                out var vPerfectCameraOffset);

            Vector3 vRealCameraPosition = _prefCameraBall.Pose.Position;
            _computeCameraDirection(
                vRealCameraPosition,
                vCarrotPos,                
                out var vRealCameraFront,
                out var vRealCameraRight,
                out var vRealCameraUp,
                out var qRealCameraOrientation
                );

            /*
             * Compute real camera object velocity for audio. 
             */
            _computeCameraVelocity(dt, vRealCameraPosition);

            float cameraDist = (vRealCameraPosition - vCarrotPos).Length();
            Trace($"cameraDist = {cameraDist}");
            
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
            //var vUserCameraFront = 
            //    vRealCameraFront * (float)Math.Cos(-angleY)
            //    + vRealCameraRight * (float)Math.Sin(-angleY);
            
            _vPreviousCameraOffset = vPerfectCameraOffset;
            _vPreviousCameraPosition = vRealCameraPosition;

            /*
             * Apply relative mouse movement
             */
            var rotUp = Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), angleX);
            var rotRight = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), angleY);
            var qUserCameraOrientation = qRealCameraOrientation;
            qUserCameraOrientation *= rotRight;
            qUserCameraOrientation *= rotUp;

            /*
             * Now we have computed the position we want to target the camera object to.
             */
            lock (_engine.Simulation)
            {
                _prefCameraBall.Pose.Orientation = qUserCameraOrientation;
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
