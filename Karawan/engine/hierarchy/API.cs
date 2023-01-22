using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.engine.hierarchy
{
    public class API
    {
        private engine.Engine _engine;

        private bool _isDirty;
        private systems.NewParentSystem _newParentSystem;

        /**
         * Set a (new) parent for the corresponding entity.
         */
        public void SetParent( 
            DefaultEcs.Entity entity,
            DefaultEcs.Entity? newParent )
        {
            if( newParent == entity )
            {
                throw new ArgumentException("SetParent(): parent entity cannot be the same entity.");
            }

            /*
             * 1. Read any current parent component. If it exists, store it in oldParent
             * 2. if Write the new parent into a NewParent component, even if it is null.
             */
            if( entity.Has<components.Parent>() )
            {
                var oldParent = entity.Get<components.Parent>();
                /*
                 * Avoid pointless sets that we can discover here.
                 */
                if( newParent == oldParent.Entity )
                {
                    return;
                }
                entity.Set<components.OldParent>(new components.OldParent(oldParent));
            }
            entity.Set<components.NewParent>(new components.NewParent(newParent));

            _isDirty = true;
        }


        public void Update()
        {
            if(_isDirty)
            {
                _newParentSystem.Update(_engine);
                _isDirty = false;
            }
        }

        public API( engine.Engine engine )
        {
            _engine = engine;
            _newParentSystem = new systems.NewParentSystem(_engine);
        }
    }
}
