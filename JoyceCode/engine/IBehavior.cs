using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IBehavior
    {
        /**
         * Called after a given period of inactivity: Sync with reality before
         * continueing your behavior.
         */
        public void Sync(in DefaultEcs.Entity entity);
        
        /**
         * Called per logical frame: Do your behavior.
         */
        public void Behave(in DefaultEcs.Entity entity, float dt);
    }
}
