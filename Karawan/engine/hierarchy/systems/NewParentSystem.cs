using DefaultEcs;
using Karawan.engine.hierarchy.components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.engine.hierarchy.systems
{
    /**
     * Update the hierarchy entites after any change.
     */
    [DefaultEcs.System.With(typeof(NewParent))]
    sealed class NewParentSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;


        protected override void PreUpdate(Engine state)
        {
        }


        protected override void Update(Engine state, ReadOnlySpan<Entity> entities)
        {
            /*
             * We need to iterate through a copy of the entities because delete components.
             */
            Span<Entity> copy = stackalloc Entity[entities.Length]; // be carefull if you can actually allocate on the stack
            entities.CopyTo(copy);
            foreach( var entity in copy )
            {
                /*
                 * If I have an oldParent, remove myself from the list of children of the old parent,
                 * remove the OldParent component
                 */
                if (entity.Has<components.OldParent>())
                {
                    var oldParent = entity.Get<components.OldParent>();
                    /*
                     * OldParent must have a children list.
                     */
                    var oldParentsChildren = oldParent.Entity.Get<Children>();
                    oldParentsChildren.Entities.Remove(entity);
                    entity.Remove<components.OldParent>();

                    /*
                     * We do not re-set the Parent component here, we will do it writing the new parent
                     */
                }

                /*
                 * Write new parent
                 */
                {
                    var newParent = entity.Get<components.NewParent>();
                    if( null != newParent.Entity )
                    {
                        entity.Set<components.Parent>( new components.Parent( newParent.Entity.GetValueOrDefault() ) );
                    } else
                    {
                        entity.Remove<components.Parent>();
                    }
                    entity.Remove<components.NewParent>();
                }

            }
        }


        protected override void PostUpdate(Engine state)
        {
        }


        public NewParentSystem( engine.Engine engine )
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
        }
    }
}
