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

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);

            _prefTarget.ApplyImpulse(new Vector3(0f, 9.81f, 0f), new Vector3(0f, 0f, 0f));   
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


        public WASDPhysics(in engine.Engine engine, in DefaultEcs.Entity eTarget)
        {
            _engine = engine;
            _eTarget = eTarget;
            _aTransform = _engine.GetATransform();

        }
    }
}
