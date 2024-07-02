using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce
{
    public class HierarchyApi
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


        private void _deleteRecursively(DefaultEcs.Entity entity)
        {
            var eChildren = _engine.GetEcsWorld().GetEntities()
                .With((in components.Parent cParent) => cParent.Entity == entity).AsEnumerable();
            foreach (var eChild in eChildren)
            {
                _deleteRecursively(eChild);
                eChild.Dispose();
            }
        }
        
        
        /**
         * Recursively dispose an entity, including its children.
         */
        public void Delete(ref DefaultEcs.Entity entity)
        {
            _deleteRecursively(entity);
            entity = default;
        }
        

        public void Update()
        {
            if(_isDirty)
            {
                _newParentSystem.Update(_engine);
                _isDirty = false;
            }
        }

        public HierarchyApi()
        {
            _engine = I.Get<Engine>();
            _newParentSystem = new systems.NewParentSystem();
        }
    }
}
