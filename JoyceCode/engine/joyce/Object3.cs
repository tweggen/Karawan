using System;
using System.Collections.Generic;
using System.Text;

namespace engine.joyce
{
    public class Object3
    {
        private DefaultEcs.Entity _entity;


        private void _initEntity()
        { 
        }

        /**
         * Create an instance using an existing entity.
         */
        public Object3(DefaultEcs.Entity entity)
        {
            _entity = entity;
            _initEntity();
        }

        /**
         * Create an instance creating a new entity.
         */
        public Object3(World3 world)
        {
            _entity = world.GetEngine().CreateEntity();
            _initEntity();
        }

    }
}
