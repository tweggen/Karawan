using System;
using System.Numerics;
using engine.joyce.components;

namespace engine.joyce.systems;

[DefaultEcs.System.Without(typeof(joyce.components.Parent))]
[DefaultEcs.System.With(typeof(Transform3ToParent))]
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
     *
     * @param shallBeVisible
     *     My parent allows me to be visible at all.
     */
    private bool _updateChildToWorld(
        in components.Transform3ToWorld cParentTransform3ToWorld,
        DefaultEcs.Entity eMe,
        ref components.Transform3ToWorld cMyTransformToWorld,
        bool shallBeVisible)
    {
        ref var cTransform3 = ref eMe.Get<Transform3ToParent>();
        
        /*
         * Now I am visible if:
         * - shallBeVisible && c.Transform3.IsVisible
         * My child is supposed to be visible
         * - if PassVisible
         *    shallBeVisible && c.Transform3.IsVisible
         * - if !PassVisible
         *    true
         */
        
        /*
         * New mode of operation: Child's camera is not affected by parents or children, just left as is.
         */
        cMyTransformToWorld.CameraMask = cTransform3.CameraMask;
        cMyTransformToWorld.IsVisible = shallBeVisible && cTransform3.IsVisible; 
        cMyTransformToWorld.Matrix =
            cTransform3.Matrix * cParentTransform3ToWorld.Matrix;
        eMe.Set<Transform3ToWorld>(cMyTransformToWorld);
        return !cTransform3.PassVisibility || cMyTransformToWorld.IsVisible ;
    }


    /**
     * Recurse into the children of this entity.
     * Given
     * - entity: The entity to recurse into
     * - shallBeVisible: If we (the entity) are supposed to be visible at all. We can become invisible if an
     *   entity has PassVisibility set and is invisible.
     */
    private void _recurseChildren(
        in components.Transform3ToWorld cParentTransform3ToWorld,
        DefaultEcs.Entity entity,
        bool shallBeVisible)
    {
        var children = entity.Get<joyce.components.Children>().Entities;
        
        foreach (var childEntity in children)
        {
            if (childEntity.IsAlive)
            {
                var cChildTransform3ToWorld = new Transform3ToWorld();
                bool shallChildrenBeVisible = _updateChildToWorld(
                    cParentTransform3ToWorld, childEntity, ref cChildTransform3ToWorld, shallBeVisible);
                
                if (childEntity.Has<joyce.components.Children>()
                    && childEntity.Has<Transform3ToParent>())
                {
                    _recurseChildren(cChildTransform3ToWorld, childEntity, shallChildrenBeVisible);
                }
            }
        }
    }


    protected override void Update(Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        var cRootTransform3World = new components.Transform3ToWorld(0xffffffff, Transform3ToWorld.Visible, Matrix4x4.Identity);

        /*
         * We are iterating through all of the root objects with children
         * and transformations attached.
         */
        foreach (var entity in entities)
        {
            ref var cTransform3 = ref entity.Get<Transform3ToParent>();
            bool shallBeVisible;

            if (cTransform3.IsVisible)
            {
                shallBeVisible = true;
            }
            else
            {
                if (cTransform3.PassVisibility)
                {
                    shallBeVisible = false;
                }
                else
                {
                    shallBeVisible = true;
                }
            }

            /*
             * Update the root itself. The root's parent always is visible.
             */
            var cMyTransformToWorld = new Transform3ToWorld();
            _updateChildToWorld(cRootTransform3World, entity, ref cMyTransformToWorld, shallBeVisible);
            if (entity.Has<joyce.components.Children>())
            {
                _recurseChildren(cMyTransformToWorld, entity, shallBeVisible);
            }
        }
    }


    protected override void PostUpdate(Engine state)
    {
    }


    public PropagateTranslationSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}

