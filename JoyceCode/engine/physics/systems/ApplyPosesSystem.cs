using BepuPhysics;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.systems
{
    [DefaultEcs.System.With(typeof(components.Body))]
    internal class ApplyPosesSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private engine.Engine _engine;
        private engine.transform.API _aTransform;

        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                {
                    BodyReference pref = entity.Get<components.Body>().Reference;
                    var position = pref.Pose.Position;
                    //var orientation = Quaternion.Inverse(pref.Pose.Orientation);
                    var orientation = pref.Pose.Orientation;
                    _aTransform.SetTransform(entity, orientation, position);
                }
            }
        }

        protected override void PostUpdate(float dt)
        {

        }


        protected override void PreUpdate(float dt)
        {
            _aTransform = I.Get<engine.transform.API>();
        }

        public ApplyPosesSystem(in engine.Engine engine)
                : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
