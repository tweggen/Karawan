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

        private void _onLogicalFrame(object sender, float dt)
        {
            /*
             * We allow the user to move the cam.
             */
            Vector2 vMouseMove;
            _engine.GetMouseMove(out vMouseMove);

            if( !_eCarrot.Has<engine.transform.components.Transform3ToParent>()
                |!_eCarrot.Has<engine.transform.components.Transform3>() )
            {
                return;
            }
            var cToParent = _eCarrot.Get<engine.transform.components.Transform3ToParent>();
            var cCarrotTransform3 = _eCarrot.Get<engine.transform.components.Transform3>();
            var cTargetTransform3 = _eTarget.Get<engine.transform.components.Transform3>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);

            var vCarrotPos = cToParent.Matrix.Translation;

            // TXWTODO: This is hard coded and static, make it softer.
            var vCameraPos = vCarrotPos - 8f * vFront + 2f * vUp;
            var vTargetRotation = cCarrotTransform3.Rotation;
            var vRotation = Quaternion.Slerp(cTargetTransform3.Rotation, cCarrotTransform3.Rotation, 0.1f);
            _aTransform.SetTransform(_eTarget, vRotation, vCameraPos );
            
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
