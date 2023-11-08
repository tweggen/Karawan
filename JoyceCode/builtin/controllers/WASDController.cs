using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SkiaSharp;

namespace builtin.controllers
{
    public class WASDController
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _entity;
        private engine.joyce.TransformApi _aTransform;

        public float SideMetersPerSecond { get; set; } = 50f;
        public float FrontMetersPerSecond { get; set; } = 50f;
        public float TurnMouseXScale { get; set; } = 0.1f;

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.I.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);
            engine.I.Get<builtin.controllers.InputController>().GetMouseMove(out var vMouseMove);

            var cTransform3 = _entity.Get<engine.joyce.components.Transform3>();
            var cToParent = _entity.Get<engine.joyce.components.Transform3ToParent>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            engine.geom.Camera.VectorsFromMatrix(cToParent.Matrix, out var vFront, out var vUp, out var vRight);
            bool haveChange = false;
            /*
             * If we are moving to front/back with the controller, just change the translation.
             * If there is translation going on, apply rotation to the rotation quaternion.
             */
            var frontMotion = controllerState.WalkForward - controllerState.WalkBackward;
            if (frontMotion != 0f)
            {
                float meterPerSecond = 50f;
                cTransform3.Position += vFront * (frontMotion / 256f * (FrontMetersPerSecond * dt) );
                haveChange = true;
            }
#if false
            var turnMotion = controllerState.TurnRight - controllerState.TurnLeft;
            if (turnMotion != 0f)
            {
                float radiansPerSecond = (float)Math.PI / 2f;
                cTransform3.Rotation =
                    Quaternion.Concatenate(
                        cTransform3.Rotation,
                        Quaternion.CreateFromAxisAngle(vUp, turnMotion / 256f * radiansPerSecond * dt));
                haveChange = true;
            }
#else
            var sideMotion = controllerState.TurnRight - controllerState.TurnLeft;
            if( sideMotion != 0f)
            {
                float meterPerSecond = 50f;
                cTransform3.Position += vRight * (sideMotion / 256f * (SideMetersPerSecond * dt));
                haveChange = true;
            }
#endif
#if true
            var turnMotion = (float)vMouseMove.X * TurnMouseXScale / 180.0f * (float)Math.PI;
            if (turnMotion != 0f)
            {
                cTransform3.Rotation =
                    Quaternion.Concatenate(
                        cTransform3.Rotation,
                        Quaternion.CreateFromAxisAngle(vUp, turnMotion));
                haveChange = true;
            }
#endif
            if (haveChange)
            {
                _aTransform.SetTransforms(_entity, cTransform3.IsVisible, cTransform3.CameraMask, cTransform3.Rotation, cTransform3.Position);
                _entity.Set<engine.joyce.components.Transform3>(cTransform3);
            }
        }

        public void DeactivateController()
        {
            _engine.OnLogicalFrame -= _onLogicalFrame;
        }

        public void ActivateController()
        {
            _engine.OnLogicalFrame += _onLogicalFrame;
        }

        public WASDController(engine.Engine engine0, DefaultEcs.Entity entity)
        {
            _engine = engine0;
            _entity = entity;
            _aTransform = engine.I.Get<engine.joyce.TransformApi>();
        }
    }
}
