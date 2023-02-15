using BepuPhysics;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using DefaultEcs;

namespace engine.physics.systems
{
    /**
     * Read the world transform position of the entities and set it to the kinetics.
     */
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(engine.physics.components.Kinetic))]
    internal class MoveKineticsSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private engine.Engine _engine;
        private engine.transform.API _aTransform;

        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                {
                    entity.Get<physics.components.Kinetic>().Reference.Pose.Position =
                        entity.Get<transform.components.Transform3ToWorld>().Matrix.Translation;
                }
            }
        }

        protected override void PostUpdate(float dt)
        {

        }


        protected override void PreUpdate(float dt)
        {
            _aTransform = _engine.GetATransform();
        }

        public MoveKineticsSystem(in engine.Engine engine)
                : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
