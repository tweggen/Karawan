using DefaultEcs;
using DefaultEcs.Internal;
using DefaultEcs.Resource;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using static engine.Logger;

namespace nogame.playerhover
{
    public class WASDPhysics
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _eTarget;
        private engine.transform.API _aTransform;

        private BepuPhysics.BodyReference _prefTarget;

        private float _massShip;

        private DefaultEcs.Entity _ePhysDisplay;

        private readonly float LinearThrust = 100f;
        private readonly float AngularThrust = 1.8f;
        
        private bool _hadCollision = false;
        public void HadCollision()
        {
            _hadCollision = true;
        }
        
        private void _onLogicalFrame(object sender, float dt)
        {
            // if (_hadCollision) return;
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);

            Vector3 vTotalImpulse = new Vector3(0f, 9.81f, 0f);
            
            Vector3 vTotalAngular = new Vector3(0f, 0f, 0f);

            /*
             * Balance height first.
             *
             * Create an impulse appropriate to work against the
             * gravity. Note, that the empty impulse is pre-set with
             * an impulse to accelerate against gravity.
             */
            Vector3 vTargetPos = _prefTarget.Pose.Position;
            Vector3 vTargetVelocity = _prefTarget.Velocity.Linear;
            Vector3 vTargetAngularVelocity = _prefTarget.Velocity.Angular;
            float heightAtTarget = engine.world.MetaGen.Instance().Loader.GetNavigationHeightAt(vTargetPos);
            {
                var properDeltaY = 0;
                var deltaY = vTargetPos.Y - (heightAtTarget+properDeltaY);
                const float threshDiff = 0.01f;

                Vector3 impulse = new Vector3(0f,9.81f,0f);
                float properVelocity = 0f;
                if ( deltaY < -threshDiff )
                {
                    properVelocity = 4f; // 1ms-1 up.
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
            var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            //var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);
            var frontMotion = controllerState.FrontMotion;

            if (frontMotion != 0f)
            {
                // The acceleration looks wrong when combined with rotation.
                vTotalImpulse += LinearThrust * vFront * frontMotion / 256f;
            }
            var turnMotion = controllerState.TurnRight - controllerState.TurnLeft;
            if (turnMotion != 0f)
            {
                // TXWTODO: This does a turn that looks ríght.
                vTotalAngular += new Vector3(0f, AngularThrust * -turnMotion / 256f, 0f);
            }

            /*
             * Now apply a damping on velocity, i.e. computing linear and angular impulses
             * proportional to the velocity
             */
            // vTotalImpulse += vTargetVelocity * -0.8f;
            // vTotalAngular += vTargetAngularVelocity * -0.8f;

            /*
             * Also, try to rotate me back to horizontal plane.
             * Look, where my local up vector points and find the shortes rotation to get it back up.
             * Do that by adding some angular impulse, making it swing in the end.
             */

            /*
             * We have two vectors, the perfect up vector and the real up vector.
             * If the real up vector only differs in minimal fractions from the perfect 
             * one, we ignore the difference.
             * 
             * If there is a larger deviation, we apply an angular impulse. The axis for 
             * this impulse is the cross product of both of the up vectors.
             */
            Vector3 vSpinTopAxis = Vector3.Cross(vUp, new Vector3(0f, 1f, 0f));
            if (vSpinTopAxis.Length() > 0.01f)
            {
                vTotalAngular += vSpinTopAxis*0.9f;
            }


            /*
             * Finally, clip the height with the ground.
             */
            if( vTargetPos.Y < (heightAtTarget) )
            {
                vTargetPos.Y = heightAtTarget;
                _prefTarget.Pose.Position = vTargetPos;
                vTotalImpulse += new Vector3(0f, 10f, 0f);
            }

            var mass = 500f;
            if (vTotalImpulse.Length() > mass)
            {
                Trace($"Too fast: {vTotalImpulse.Length()}.");
            }
            if (vTotalAngular.Length() > mass)
            {
                Trace($"Too fast: {vTotalAngular.Length()}.");
            }
            
            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massShip, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massShip);

            _ePhysDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(20f, 240f),
                new Vector2(100, 16),
                $"x: {vTargetPos.X}, y: {vTargetPos.Y}, z: {vTargetPos.Z}",
                10,
                0xff22aaee,
                0x00000000
            ));
            
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
            _ePhysDisplay = _engine.CreateEntity("OsdPhysDisplay");
            _massShip = massShip;
        }
    }
}
