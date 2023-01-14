using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.cause
{
    /**
     * This creates and maintains the actual universe.
     */
    class Total
    {
        engine.Engine _engine;

        private void _bigBangFromScratch()
        {
            /*
             * Big bang it.
             */
            var rootFrame = _engine.GetEcsWorld().CreateEntity();

        }

        public Total( engine.Engine engine )
        {
            _engine = engine;

        }
    }
}
