using System;
using DefaultEcs;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.systems
{
    [DefaultEcs.System.With(typeof(components.Behavior))]
    [DefaultEcs.System.With(typeof(transform.components.Transform3ToWorld))]
    internal class WiperSystem : DefaultEcs.System.AEntitySetSystem<IList<Vector3>>
    {
        private engine.Engine _engine;

        protected override void Update(
            IList<Vector3> aabb, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            /*
             * We'll eventually remove entities.
             */
            Span<Entity> copy = stackalloc Entity[entities.Length];
            entities.CopyTo(copy);
            foreach (var entity in copy)
            {
                Vector3 pos = entity.Get<transform.components.Transform3ToWorld>().Matrix.Translation;
                if( pos.X >= aabb[0].X && pos.Y >= aabb[0].Y && pos.Z > aabb[0].Z
                    && pos.X <= aabb[1].X && pos.Y <= aabb[1].Y && pos.Z < aabb[1].Z )
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
