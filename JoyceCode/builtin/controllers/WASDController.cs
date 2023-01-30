using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace builtin.controllers
{
    public class WASDController
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _entity;
        private engine.transform.API _aTransform;

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);
            var cTransform3 = _entity.Get<engine.transform.components.Transform3>();
            var cToParent = _entity.Get<engine.transform.components.Transform3ToParent>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3( -cToParent.Matrix.M13, -cToParent.Matrix.M23, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M12, cToParent.Matrix.M22, cToParent.Matrix.M32);
            bool haveChange = false;
            /*
             * If we are moving to front/back with the controller, just change the translation.
             * If there is translation going on, apply rotation to the rotation quaternion.
             */
            var frontMotion = controllerState.WalkForward - controllerState.WalkBackward;
            if (frontMotion != 0f)
            {
                float meterPerSecond = 100f;
                cTransform3.Position += vFront * (frontMotion * (meterPerSecond * dt) / 256f);
                haveChange = true;
            }
            var turnMotion = controllerState.TurnRight - controllerState.TurnLeft;
            if (turnMotion != 0f)
            {
                float radiansPerSecond = (float)Math.PI * 2f;
                cTransform3.Rotation = Quaternion.Concatenate(cTransform3.Rotation, Quaternion.CreateFromAxisAngle(vUp, radiansPerSecond * dt * turnMotion));
                haveChange = true;
            }
            if (haveChange)
            {
                _aTransform.SetTransforms(_entity, cTransform3.IsVisible, cTransform3.CameraMask, cTransform3.Rotation, cTransform3.Position);
                _entity.Set<engine.transform.components.Transform3>(cTransform3);
            }
        }

        public void DeactivateController()
        {
            _engine.LogicalFrame -= _onLogicalFrame;
        }

        public void ActivateController()
        {
            _engine.LogicalFrame += _onLogicalFrame;
        }

        public WASDController(engine.Engine engine, DefaultEcs.Entity entity)
        {
            _engine = engine;
            _entity = entity;
            _aTransform = _engine.GetATransform();
        }
    }
}
