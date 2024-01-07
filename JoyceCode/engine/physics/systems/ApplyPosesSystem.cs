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
        private engine.joyce.TransformApi _aTransform;

        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                ref physics.components.Body cRefBody = ref entity.Get<physics.components.Body>();
                var po = cRefBody.PhysicsObject;
                if (po == null) continue;
            
                // TXWTODO: This is a workaround for addressing only the former kinematic objects.
                if ((po.Flags & (physics.Object.HaveContactListener|physics.Object.IsStatic)) != physics.Object.HaveContactListener)
                {
                    continue;
                }
            
                Vector3 vPosition;
                Quaternion qOrientation;
                lock (_engine.Simulation)
                {
                    BodyReference pref = cRefBody.Reference;
                    vPosition = pref.Pose.Position;
                    qOrientation = pref.Pose.Orientation;
                }
                _aTransform.SetTransform(entity, qOrientation, vPosition);
            }
        }

        protected override void PostUpdate(float dt)
        {

        }


        protected override void PreUpdate(float dt)
        {
            _aTransform = I.Get<engine.joyce.TransformApi>();
        }

        
        public ApplyPosesSystem(in engine.Engine engine)
                : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
