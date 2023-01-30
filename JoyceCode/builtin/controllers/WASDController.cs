using System;
using System.Collections.Generic;
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
            engine.transform.components.Transform3 transform3 = _aTransform.GetTransform(_entity);

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
        }
    }
}
