﻿
namespace engine.joyce.components
{
    struct NewParent
    {
        public DefaultEcs.Entity? Entity;

        public NewParent( in DefaultEcs.Entity? newParentEntity )
        {
            Entity = newParentEntity;
        }
    }
}
