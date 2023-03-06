

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

                float distance = vRelativePos.Length();
                if (distance < 1f) distance = 1f;
                float volumeAdjust = 1f / distance;

                Vector3 vListenerVelocity = (_listenerPosition - _previousListenerPosition) / dt;
                Vector3 vSourceVelocity = entity.Get<engine.joyce.components.Motion>().Velocity;
                Vector3 vSoundDirection = vSourceVelocity - vListenerVelocity;
                float lengthSoundDirection = vSoundDirection.Length();
                float projectedListenerVelocity, projectedSourceVelocity;
                if (Math.Abs(lengthSoundDirection) > 0.001)
                {
                    projectedListenerVelocity =
                        Vector3.Dot(vListenerVelocity, vSoundDirection) / lengthSoundDirection;
                    projectedSourceVelocity =
                        Vector3.Dot(vSourceVelocity, vSoundDirection) / lengthSoundDirection;
                }
                else
                {
                    projectedListenerVelocity = vListenerVelocity.Length();
                    projectedSourceVelocity = vSourceVelocity.Length();
                }

                float freqFactor;
                if (Math.Abs(343f + projectedSourceVelocity) < 0.001)
                {
                    /*
                     * supersonic boom.
                     */
                    freqFactor = 0f;
                }
                else
                {
                    freqFactor = (343f + projectedListenerVelocity) / (343f + projectedSourceVelocity);
                }

                var cMovingSound = entity.Get<engine.audio.components.MovingSound>();

                if (distance > cMovingSound.MaxDistance)
                {
                    cMovingSound.MotionVolume = 0f;
                }
                else
                {
                    cMovingSound.MotionVolume = volumeAdjust;
                    cMovingSound.MotionPitch = freqFactor;
                }

                entity.Set( cMovingSound );
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