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
        private DefaultEcs.Entity _entity;
        private engine.transform.API _aTransform;

        private void _onLogicalFrame(object sender, float dt)
        {
            engine.ControllerState controllerState;
            _engine.GetControllerState(out controllerState);
        }

        public void DeactivateController()
        {
            _engine.LogicalFrame -= _onLogicalFrame;
        }

        public void ActivateController()
        {
            _engine.LogicalFrame += _onLogicalFrame;
        }


        public WASDPhysics(in engine.Engine engine, in DefaultEcs.Entity entity)
        {
            _engine = engine;
            _entity = entity;
            _aTransform = _engine.GetATransform();

        }
    }
}
