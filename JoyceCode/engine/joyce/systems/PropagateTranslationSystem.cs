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
     */
    private void _updateChildToWorld(
        in components.Transform3ToWorld cParentTransform3ToWorld,
        DefaultEcs.Entity childEntity,
        ref components.Transform3ToWorld cChildTransform3ToWorld)
    {
        var cTransform3 = childEntity.Get<Transform3ToParent>();

#if true
        /*
         * New mode of operation: Child's camera is not affected by parents or children, just left as is.
         */
        cChildTransform3ToWorld.CameraMask = cTransform3.CameraMask;
#else
            /*
             * Child's world camera mask is parent's camera mask and its own combinied with its visibility.
             */
            cChildTransform3ToWorld.CameraMask =
                cParentTransform3ToWorld.CameraMask &
                    (cTransform3.IsVisible ? cTransform3.CameraMask : 0);
#endif
        cChildTransform3ToWorld.Matrix =
            cTransform3.Matrix * cParentTransform3ToWorld.Matrix;
        childEntity.Set<Transform3ToWorld>(cChildTransform3ToWorld);
    }


    private void _recurseChildren(
        in components.Transform3ToWorld cParentTransform3ToWorld,
        DefaultEcs.Entity entity)
    {
        var children = entity.Get<joyce.components.Children>().Entities;
        // Console.WriteLine("nChildren = {0}", children.Count);
        foreach (var childEntity in children)
        {
            var cChildTransform3ToWorld = new Transform3ToWorld();
            _updateChildToWorld(cParentTransform3ToWorld, childEntity, ref cChildTransform3ToWorld);

            if (childEntity.Has<joyce.components.Children>()
                && childEntity.Has<Transform3ToParent>())
            {
                _recurseChildren(cChildTransform3ToWorld, childEntity);
            }
        }
    }


    protected override void Update(Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        var cRootTransform3World = new components.Transform3ToWorld(0xffffffff, Matrix4x4.Identity);

        /*
         * We are iterating through all of the root objects with children
         * and transformations attached.
         */
        foreach (var entity in entities)
        {
            /*
             * Update the root itself. The root's parent always is visible.
             */
            var cChildTransform3ToWorld = new Transform3ToWorld();
            _updateChildToWorld(cRootTransform3World, entity, ref cChildTransform3ToWorld);
            if (entity.Has<joyce.components.Children>())
            {
                _recurseChildren(cChildTransform3ToWorld, entity);
            }
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

