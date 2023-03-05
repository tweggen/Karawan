

using System;
using System.Numerics;

namespace engine.audio.systems
{

    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(engine.joyce.components.Motion))]
    sealed public class MovingSoundsSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private object _lo = new();

        private Vector3 _previousListenerPosition;
        private Vector3 _listenerPosition;
        private engine.Engine _engine;

        protected override void PreUpdate(float dt)
        {
        }

        protected override void PostUpdate(float dt)
        {
        }
        
        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            if (dt < 0.001f)
            {
                return;
            }
            Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntities);
            foreach (var entity in copiedEntities)
            {
                /*
                 * For each entity, compute the volume and sound adjustment
                 * from the distance.
                 */
                Vector3 vEntityPos = entity.Get<engine.transform.components.Transform3ToWorld>().Matrix.Translation;
                Vector3 vRelativePos = vEntityPos - _listenerPosition;

                float dist2 = vRelativePos.Length();
                if (dist2 < 1f) dist2 = 1f;
                float volumeAdjust = 1f / dist2;

                Vector3 vListenerVelocity = (_listenerPosition - _previousListenerPosition) / dt;
                Vector3 vTargetVelocity = entity.Get<engine.joyce.components.Motion>().Velocity;

                float listenerVelocity = vListenerVelocity.Length();
                float targetVelocity = vTargetVelocity.Length();
                Vector3 vRelativeVelocity = vTargetVelocity - vListenerVelocity;
                float relativeVelocity = vRelativeVelocity.Length();
                float scalar = Vector3.Dot(vListenerVelocity, vTargetVelocity);
                float cosTarget;
                    if( Math.Abs(relativeVec) < 0.001 || Math.Abs()
                    = scalar / listenerVelocity / targetVelocity; 
                

            }
        }

        public void SetListenerPosition(in Vector3 listenerPosition)
        {
            lock (_lo)
            {
                _previousListenerPosition = _listenerPosition;
                _listenerPosition = listenerPosition;
            }
        }
        
        public MovingSoundsSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}