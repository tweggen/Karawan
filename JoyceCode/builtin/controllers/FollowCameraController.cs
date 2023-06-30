using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace builtin.controllers
{
    public class FollowCameraController
    {
        engine.Engine _engine;
        DefaultEcs.Entity _eTarget;
        DefaultEcs.Entity _eCarrot;
        engine.transform.API _aTransform;
        private Vector3 _vPreviousCameraPos;

        Quaternion _qCameraRotation;
        Vector2 _vMouseOffset;
        float _lastMouseMove = 0f;
        private bool _firstFrame = true;
        
        private void _onLogicalFrame(object sender, float dt)
        {
            /*
             * We allow the user to move the cam.
             */
            Vector2 vMouseMove;
           _engine.GetMouseMove(out vMouseMove);
           _engine.GetControllerState(out var controllerState);

            if( !_eCarrot.Has<engine.transform.components.Transform3ToWorld>()
                || !_eCarrot.Has<engine.transform.components.Transform3>())
            {
                return;
            }
            var cToParent = _eCarrot.Get<engine.transform.components.Transform3ToWorld>();
            var cCarrotTransform3 = _eCarrot.Get<engine.transform.components.Transform3>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);
            var angleX = -_vMouseOffset.Y * (float)Math.PI / 180f;
            var angleY = -_vMouseOffset.X * (float)Math.PI / 180f;
            
            var vCarrotPos = cToParent.Matrix.Translation;

            /*
             * Compute a distance from the zoom state.
             * TXWTODO: Remove the magic numbers.
             */
            var zoomFactor = controllerState.ZoomState > 0 ? controllerState.ZoomState * 2f : controllerState.ZoomState;
            float zoomDistance = (20f + (float)zoomFactor) / 2f;
            // var vCameraFront = vFront;
            var vCameraFront = vFront * (float)Math.Cos(-angleY) + vRight * (float)Math.Sin(-angleY);
            var vCameraDirection = zoomDistance * vCameraFront - zoomDistance/4f * vUp;
  
            var vCameraPos = vCarrotPos - vCameraDirection;
            if (!_firstFrame)
            {
                var vCameraVelocity = (vCameraPos - _vPreviousCameraPos) / dt;
                _eTarget.Set(new engine.joyce.components.Motion(vCameraVelocity));
                _vPreviousCameraPos = vCameraPos;
            }

            // var vCarrotRotation = cCarrotTransform3.Rotation;
            var qRotation = Quaternion.Slerp(_qCameraRotation, cCarrotTransform3.Rotation, 0.1f);
            _qCameraRotation = qRotation;

            /*
             * Some up the relative mouse movement.
             */
            _vMouseOffset += vMouseMove * 0.05f;
            if( vMouseMove.X == 0f && vMouseMove.Y == 0f )
            {
                _lastMouseMove += dt;
            }
            else
            {
                _lastMouseMove = 0f;
            }

            /*
             * Apply relative mouse movement
             */
            var rotUp = Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), angleX);
            var rotRight = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), angleY);
            qRotation *= rotRight;
            qRotation *= rotUp;
            _aTransform.SetTransform(_eTarget, qRotation, vCameraPos );

            /*
             * ramp down mouse offset after 1.5s of inactivity.
             */
            if( _lastMouseMove > 1.5f )
            {
                _vMouseOffset *= 0.98f;
            }

            _firstFrame = false;
        }

        public void DeactivateController()
        {
            _engine.LogicalFrame -= _onLogicalFrame;
        }

        public void ActivateController()
        {
            _engine.LogicalFrame += _onLogicalFrame;
        }

        public FollowCameraController(engine.Engine engine, DefaultEcs.Entity eTarget, DefaultEcs.Entity eCarrot) 
        {
            _engine = engine;
            _eTarget = eTarget;
            _eCarrot = eCarrot;
            _aTransform = _engine.GetATransform();
        }
    }
}
