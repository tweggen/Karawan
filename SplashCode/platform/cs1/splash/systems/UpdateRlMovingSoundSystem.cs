
using DefaultEcs.Resource;
using System;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    sealed public class UpdateRlMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        
        /**
         * Schedule a sound entry for later deletion in the engine.
         * It may or may not be reused.
         */
        private void _queueUnloadSoundEntry(in RlSoundEntry rlSoundEntry)
        {
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
                bool hasRlSound = entity.Has<components.RlSound>();
                var cMovingSound = entity.Get<engine.audio.components.MovingSound>();
                components.RlSound cRlSound;

               if (hasRlSound)
                {
                    cRlSound = entity.Get<components.RlSound>();
                    RlSoundEntry rlSoundEntry = cRlSound.SoundEntry;

                    /*
                     * Caution: If the rlSoundEntry is null, the sound still is loading.
                     * We neither can modify or unload it.
                     */
                    if (rlSoundEntry == null)
                    {
                        continue;
                    }

                    if (cMovingSound.MotionVolume == 0)
                    {
                        /*
                         * Be safe and stop it.
                         */
                        Raylib_CsLo.Raylib.StopSound(rlSoundEntry.RlSound);

                        /*
                         * Immediately remove the sound from this entity, but asynchronously
                         * remove it from the manager.
                         */
                        entity.Remove<components.RlSound>();
                        _queueUnloadSoundEntry(cRlSound.SoundEntry);
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
                        
                        Raylib_CsLo.Raylib.SetSoundVolume( rlSoundEntry.RlSound, resultingVolume );
                        Raylib_CsLo.Raylib.SetSoundPitch( rlSoundEntry.RlSound, resultingPitch );
                        Raylib_CsLo.Raylib.SetSoundPan( rlSoundEntry.RlSound, resultingPan );
                    }
                }
                else
                {
                    if (cMovingSound.MotionVolume > 0f)
                    {
                        entity.Set(new components.RlSound());
                        
                        /*
                         * We don't have a sound but need it. As Loading might be async, just
                         * schedule the loading.
                         */
                        _queueLoadMovingSoundToEntity(entity, cMovingSound);
                    }

                }
            }
        }


        public unsafe UpdateRlMovingSoundSystem(engine.Engine engine
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
