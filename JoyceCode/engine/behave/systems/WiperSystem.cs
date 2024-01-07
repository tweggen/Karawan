using System;
using DefaultEcs;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using engine.world.components;

namespace engine.behave.systems
{
    [DefaultEcs.System.With(typeof(components.Behavior))]
    [DefaultEcs.System.With(typeof(joyce.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(FragmentId))]
    internal class WiperSystem : DefaultEcs.System.AEntitySetSystem<engine.geom.AABB>
    {
        private engine.Engine _engine;

        protected override void Update(
            engine.geom.AABB aabb, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            /*
             * We'll eventually remove entities.
             */
            Span<Entity> copy = stackalloc Entity[entities.Length];
            entities.CopyTo(copy);
            foreach (var entity in copy)
            {
                Vector3 pos = entity.Get<joyce.components.Transform3ToWorld>().Matrix.Translation;
                if( aabb.Contains(pos) )
                {
                    /*
                     * Keep entity.
                     */
                } else
                {
                    /*
                     * Remove entity
                     */
                    entity.Dispose();
                }
            }
        }


        public WiperSystem(in engine.Engine engine)
                : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
