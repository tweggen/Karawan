

using System;
using System.Numerics;

namespace engine.audio.systems
{

    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    [DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(engine.joyce.components.Motion))]
    sealed public class MovingSoundsSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private object _lo = new();

        private Vector3 _vPreviousListenerPosition;
        private Vector3 _vListenerPosition;
        private Vector3 _vListenerRight;

        /**
         * We all know the speed of sound is 343 m/s.
         * However, lowering the speed of sound makes the doppler effect more spectacular.
         */
        public float SpeedOfSound { get; set; } = 150f;
        
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
                Vector3 vEntityPos = entity.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
                Vector3 vRelativePos = vEntityPos - _vListenerPosition;
                
                float distance = vRelativePos.Length();
                if (distance < 1f) distance = 1f;
                //float volumeAdjust = (float)Math.ReciprocalSqrtEstimate((double)distance);
                float volumeAdjust = Single.Min(1f,4f/distance);

                Vector3 vListenerVelocity = (_vListenerPosition - _vPreviousListenerPosition) / dt;
                Vector3 vSourceVelocity = entity.Get<engine.joyce.components.Motion>().Velocity;
                float lengthRelativePosition = vRelativePos.Length();
                float projectedListenerVelocity, projectedSourceVelocity;
                if (Math.Abs(lengthRelativePosition) > 0.001)
                {
                    projectedListenerVelocity =
                        Vector3.Dot(vListenerVelocity, vRelativePos) / lengthRelativePosition;
                    projectedSourceVelocity =
                        Vector3.Dot(vSourceVelocity, vRelativePos) / lengthRelativePosition;
                }
                else
                {
                    projectedListenerVelocity = vListenerVelocity.Length();
                    projectedSourceVelocity = vSourceVelocity.Length();
                }

                float freqFactor;
                if (Math.Abs(SpeedOfSound + projectedSourceVelocity) < 0.001)
                {
                    /*
                     * supersonic boom.
                     */
                    freqFactor = 0f;
                }
                else
                {
                    freqFactor = (SpeedOfSound + projectedListenerVelocity) / (SpeedOfSound + projectedSourceVelocity);
                }

                var cPreviousMovingSound = entity.Get<engine.audio.components.MovingSound>();
                engine.audio.components.MovingSound cNewMovingSound = new();

                if (distance > cPreviousMovingSound.MaxDistance)
                {
                    cNewMovingSound.MotionVolume = 0;
                }
                else
                {
                    cNewMovingSound.MotionVolume = (ushort)(volumeAdjust*components.MovingSound.MotionVolumeMax);
                    cNewMovingSound.MotionPitch = freqFactor;
                    float pan;
                    if (distance < 0.1f)
                    {
                        pan = 0f;
                    }
                    else
                    {
                        pan = Vector3.Dot(_vListenerRight, vRelativePos) / distance;
                    }

                    pan = (float)Math.Min(1.0, Math.Max(-1.0, pan));
                    cNewMovingSound.MotionPan = (sbyte)(pan * components.MovingSound.MotionPanMax);
                }
                
                cPreviousMovingSound.AddFrame(cNewMovingSound);
                entity.Set( cPreviousMovingSound );
            }
        }

        public void SetListenerPosRight(in Vector3 vListenerPosition, in Vector3 vListenerRight)
        {
            lock (_lo)
            {
                _vPreviousListenerPosition = _vListenerPosition;
                _vListenerPosition = vListenerPosition;
                _vListenerRight = vListenerRight;
            }
        }
        
        
        public MovingSoundsSystem()
            : base(I.Get<Engine>().GetEcsWorldAnyThread())
        {
            _engine = I.Get<Engine>();
        }
    }
}