using System;
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
            foreach( var entity in entities )
            {
                entity.Get<behave.components.Behavior>().Provider.Behave(entity, dt);
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
