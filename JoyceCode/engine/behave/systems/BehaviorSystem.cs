using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.systems
{
    [DefaultEcs.System.With(typeof(components.Behavior))]
    internal class BehaviorSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private engine.Engine _engine;

        protected override void Update(
            float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntities);
            foreach (var entity in copiedEntities)
            {
                /*
                 * We automagically update velocity from behavior.
                 * Assuming, that it modifies Transform3
                 */
                bool hadTransform3 = entity.Has<transform.components.Transform3>();
                Vector3 vOldPosition;
                if (hadTransform3)
                {
                    vOldPosition = entity.Get<transform.components.Transform3>().Position;
                }
                else
                {
                    vOldPosition = new Vector3();
                }
                entity.Get<behave.components.Behavior>().Provider.Behave(entity, dt);
                if (dt > 0.0000001 && hadTransform3)
                {
                    if (entity.Has<transform.components.Transform3>())
                    {
                        Vector3 vNewPosition = entity.Get<transform.components.Transform3>().Position;
                        /*
                         * Write back/create motion for that one.
                         */
                        Vector3 vVelocity = (vNewPosition - vOldPosition) / dt;

                        entity.Set(new joyce.components.Motion(vVelocity));
                    }
                }
            }
        }

        protected override void PostUpdate(float dt)
        {

        }


        protected override void PreUpdate(float dt)
        {

        }

        public BehaviorSystem(in engine.Engine engine )
                : base (engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
