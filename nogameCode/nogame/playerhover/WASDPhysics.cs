using DefaultEcs;
using DefaultEcs.Internal;
using DefaultEcs.Resource;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime;
using System.Text;

namespace nogame.playerhover
{
    public class WASDPhysics
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _eTarget;
        private engine.transform.API _aTransform;

        private BepuPhysics.BodyReference _prefTarget;

        private float _massShip;

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);

            Vector3 vTotalImpulse = new Vector3(0f, 9.81f, 0f);
            Vector3 vTotalAngular = new Vector3(0f, 0f, 0f);

            /*
             * Balance height first.
             */
            Vector3 vTargetPos = _prefTarget.Pose.Position;
            Vector3 vTargetVelocity = _prefTarget.Velocity.Linear;
            var heightAtTarget = engine.world.MetaGen.Instance().Loader.GetHeightAt(vTargetPos.X, vTargetPos.Z);
            {
                var properDeltaY = 3.5f;
                var deltaY = vTargetPos.Y - (heightAtTarget+properDeltaY);
                const float threshDiff = 0.01f;

                Vector3 impulse = new Vector3(0f,9.81f,0f);
                float properVelocity = 0f;
                if ( deltaY < -threshDiff )
                {
                    properVelocity = 2f; // 1ms-1 up.
                }
                else if( deltaY > threshDiff )
                {
                    properVelocity = -2f; // 1ms-1 down.
                }
                float deltaVelocity = properVelocity - vTargetVelocity.Y;
                float fireRate = deltaVelocity;
                impulse = new Vector3(0f, fireRate, 0f);
                vTotalImpulse += impulse;
            }

            /*
             * Apply controls
             */
            var cTransform3 = _eTarget.Get<engine.transform.components.Transform3>();
            var cToParent = _eTarget.Get<engine.transform.components.Transform3ToParent>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3(-cToParent.Matrix.M13, -cToParent.Matrix.M23, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M12, cToParent.Matrix.M22, cToParent.Matrix.M32);
            var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M21, cToParent.Matrix.M31);
            var frontMotion = controllerState.WalkForward - controllerState.WalkBackward;
            if (frontMotion != 0f)
            {
                float power = 20f;
                // The acceleration looks wrong when combined with rotation.
                vTotalImpulse += power * vFront * frontMotion / 256f;
            }
            var turnMotion = controllerState.TurnRight - controllerState.TurnLeft;
            if (turnMotion != 0f)
            {
                // TXWTODO: This does a turn that looks ríght.
                vTotalAngular += new Vector3(0f, -turnMotion / 256f, 0f);
            }

            /*
             * Now apply a damping on velocity.
             */
            // TXWTODO: Write me.

            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massShip, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massShip);
        }


        public void DeactivateController()
        {
            _engine.LogicalFrame -= _onLogicalFrame;
        }


        public void ActivateController()
        {
            _prefTarget = _eTarget.Get<engine.physics.components.Body>().Reference;
            
            _engine.LogicalFrame += _onLogicalFrame;
        }


        public WASDPhysics(
            in engine.Engine engine, 
            in DefaultEcs.Entity eTarget,
            in float massShip)
        {
            _engine = engine;
            _eTarget = eTarget;
            _aTransform = _engine.GetATransform();
            _massShip = massShip;
        }
    }
}
