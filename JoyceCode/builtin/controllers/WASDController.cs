using System;
using System.Collections.Generic;
using System.Text;

namespace builtin.controllers
{
    public class WASDController
    {
        private engine.Engine _engine;
        private DefaultEcs _entity;


        private void _onLogicalFrame(float dt)
        {

        }

        public void DeactivateController()
        {
            // _engine.RemoveOnLogical(this._onLogicalFrame);
        }

        public void ActivateController()
        {
            // _engine.AddOnLogical(this._onLogicalFrame);
        }

        public WASDController(engine.Engine engine, DefaultEcs.Entity entity)
        {
            _engine = engine;
            // _entity = entity;
        }
    }
}
