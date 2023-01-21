using System.Collections.Generic;

namespace Karawan.engine.hierarchy.components
{
    struct Children
    {
        public List<DefaultEcs.Entity> Entities;

        public Children( in DefaultEcs.Entity entity )
        {
            Entities = new List<DefaultEcs.Entity>();
            Entities.Add(entity);
        }
    }
}
