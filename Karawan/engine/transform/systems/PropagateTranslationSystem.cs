using System;
using System.Numerics;

namespace Karawan.engine.transform.systems
{
    [DefaultEcs.System.Without(typeof(hierarchy.components.Parent))]
    [DefaultEcs.System.With(typeof(hierarchy.components.Children))]
    [DefaultEcs.System.With(typeof(transform.components.Object3ToParentMatrix))]
    sealed class PropagateTranslationSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;


        protected override void PreUpdate(Engine state)
        {
        }

        /**
         * Compute the child's object to world matrix, given the parent's
         * object to world matrix and the child.
         * (Over-)Write the child's object to world matrix.
         */
        private void _updateChildToWorld(
            in Matrix4x4 parentObject3ToWorld, 
            DefaultEcs.Entity childEntity) 
        {
            var result =
                 childEntity.Get<transform.components.Object3ToParentMatrix>().Matrix
                 * parentObject3ToWorld;
            childEntity.Set<transform.components.Object3ToWorldMatrix>(
                new transform.components.Object3ToWorldMatrix(result));
        }

        private void _recurseChildren(DefaultEcs.Entity entity)
        {
            var children = entity.Get<hierarchy.components.Children>().Entities;
            var parentObject3ToWorld = entity.Get<transform.components.Object3ToWorldMatrix>().Matrix;
            foreach(var childEntity in children)
            {
                _updateChildToWorld(parentObject3ToWorld, childEntity);

                if( childEntity.Has<hierarchy.components.Children>() 
                    && childEntity.Has<transform.components.Object3ToParentMatrix>())
                {
                    _recurseChildren(childEntity);
                }
            }
        }

        protected override void Update(Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            /**
             * We are iterating through all of the root objects with children
             * and transformations attached.
             */
            foreach (var entity in entities)
            {
                _recurseChildren(entity);
            }
        }


        protected override void PostUpdate(Engine state)
        {
        }


        public PropagateTranslationSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}

