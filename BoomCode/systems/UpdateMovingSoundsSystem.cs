using System.Collections.Generic;
using System;

namespace Boom.systems
{
    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    sealed public class UpdateRlMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;
        private object _lo = new();

        private Queue<Boom.Sound> _queueUnloadEntries;

        private AudioPlaybackEngine _audioPlaybackEngine;
        
        /**
         * Schedule a sound entry for later deletion in the engine.
         * It may or may not be reused.
         */
        private void _queueUnloadSoundEntry(in Boom.Sound bSound)
        {
            lock (_lo)
            {
                _queueUnloadEntries.Enqueue(bSound);
            }
        }

        /**
         * Queue obtaining a sound and attaching it to this entity.
         */
        private void _queueLoadMovingSoundToEntity(
            in DefaultEcs.Entity entity,
            in engine.audio.components.MovingSound cMovingSound)
        {
            
        }

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }


        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntities);
            foreach (var entity in entities)
            {
                /*
                 * We need to iterate through all moving sounds, finally creating
                 * or updating the sounds depending on distance.
                 */
                bool hasRlSound = entity.Has<components.BoomSound>();
                var cMovingSound = entity.Get<engine.audio.components.MovingSound>();
                components.BoomSound cBoomSound;

               if (hasRlSound)
                {
                    cBoomSound = entity.Get<components.BoomSound>();
                    Boom.Sound bSound = cBoomSound.Sound;

                    /*
                     * Caution: If the rlSoundEntry is null, the sound still is loading.
                     * We neither can modify or unload it.
                     */
                    if (bSound == null)
                    {
                        continue;
                    }

                    if (cMovingSound.MotionVolume == 0)
                    {
                        /*
                         * Be safe and stop it.
                         */
                        _audioPlaybackEngine.StopSound(bSound);
                        // Raylib_CsLo.Raylib.StopSound(rlSoundEntry.RlSound);

                        /*
                         * Immediately remove the sound from this entity, but asynchronously
                         * remove it from the manager.
                         */
                        entity.Remove<components.BoomSound>();
                        _queueUnloadSoundEntry(bSound);
                    }
                    else
                    {
                        /*
                         * We have a sound and can modify it.
                         * Adjust the settings of the raylib sound by the MovingSound data.
                         */
                        float resultingVolume = cMovingSound.Sound.Volume * (float)cMovingSound.MotionVolume / 65535f;
                        float resultingPitch = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;
                        float resultingPan = (float) cMovingSound.MotionPan / 32767f;

                        bSound.Volume = resultingVolume;
                        bSound.Pan = resultingPan;
                        bSound.Speed = resultingPitch;
                    }
                }
                else
                {
                    if (cMovingSound.MotionVolume > 0f)
                    {
                        entity.Set(new components.BoomSound(null));
                        
                        /*
                         * We don't have a sound but need it. As Loading might be async, just
                         * schedule the loading.
                         */
                        _queueLoadMovingSoundToEntity(entity, cMovingSound);
                    }

                }
            }
        }


        public UpdateRlMovingSoundSystem(
            engine.Engine engine,
            AudioPlaybackEngine audioPlaybackEngine 
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            _audioPlaybackEngine = audioPlaybackEngine;
        }
    }
}
