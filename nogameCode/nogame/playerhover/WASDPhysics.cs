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

        private BepuPhysics.BodyHandle _phandleTarget;
        private BepuPhysics.BodyReference _prefTarget;

        private float _massShip;

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);

            /*
             * Balance height before clipping hard.
             */
            Vector3 vTargetPos = _prefTarget.Pose.Position;
            var heightAtTarget = engine.world.MetaGen.Instance().Loader.GetHeightAt(vTargetPos.X, vTargetPos.Z);
            {
                var properDeltaY = 3.5f;
                var deltaY = vTargetPos.Y - (heightAtTarget+properDeltaY);
                const float threshDiff = 0.01f;

                if( deltaY < -threshDiff )
                {
                    float fireRate = Math.Max(-deltaY, 1);
                    /*
                     * We need to move up. Accelerate with a bit more than g.
                     */
                    _prefTarget.ApplyImpulse(new Vector3(0f, 9.81f + fireRate*20f, 0f) * dt * _massShip, new Vector3(0f, 0f, 0f));
                }
                else if( deltaY > threshDiff )
                {
                    float fireRate = Math.Max(deltaY, 1);
                    /*
                     * We need to move down. Accelerate with a bit less than g.
                     */
                    _prefTarget.ApplyImpulse(new Vector3(0f, 9.81f - fireRate*0.5f, 0f) * dt * _massShip, new Vector3(0f, 0f, 0f));
                }
            }
        }


        public void DeactivateController()
        {
            _engine.LogicalFrame -= _onLogicalFrame;
        }


        public void ActivateController()
        {
            _phandleTarget = _eTarget.Get<engine.physics.components.Body>().Handle;
            _prefTarget = _engine.Simulation.Bodies.GetBodyReference(_phandleTarget);
            
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
