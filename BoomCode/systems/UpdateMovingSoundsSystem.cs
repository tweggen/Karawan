using System.Collections.Generic;
using System;
using static engine.Logger;

namespace Boom.systems
{
    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    sealed public class UpdateMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private object _lo = new();
        private engine.Engine _engine;
        private engine.WorkerQueue _audioWorkerQueue = new("UpdateMovingSoundSystem.Audio");

        private Queue<Boom.Sound> _queueUnloadEntries = new();

        public float MinAudibleVolume { get; set; } = 0.01f;

        /**
         * Schedule a sound entry for later deletion in the engine.
         * It may or may not be reused.
         */
        private void _queueUnloadSoundEntry(Boom.Sound bSound)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                AudioPlaybackEngine.Instance.StopSound(bSound);
            });
        }

        /**
         * Queue obtaining a sound and attaching it to this entity.
         */
        private void _queueLoadMovingSoundToEntity(
            DefaultEcs.Entity entity,
            engine.audio.components.MovingSound cMovingSound)
        {
            string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");


            _audioWorkerQueue.Enqueue(() =>
            {
                AudioPlaybackEngine.Instance.FindCachedSound(
                    resourcePath + cMovingSound.Sound.Url,
                    (Boom.CachedSound bCachedSound) =>
                    {
                        _engine.QueueMainThreadAction(() =>
                        {
                            Boom.Sound bSound = new Sound(bCachedSound);
                            entity.Set(new components.BoomSound(bSound));

                            bSound.Volume = cMovingSound.Sound.Volume * cMovingSound.MotionVolume;
                            bSound.Pan = cMovingSound.MotionPan;
                            bSound.Speed = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;
                            
                            _audioWorkerQueue.Enqueue(() =>
                            {
                                AudioPlaybackEngine.Instance.PlaySound(bSound);
                            });
                        });
                    });
            });
        }
        

        protected override void PreUpdate(float dt)
        {
        }
        
 
        protected override void PostUpdate(float dt)
        {
        }


        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            ushort minAudibleUShort = (ushort)(MinAudibleVolume * 65535f);
            Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntities);
            foreach (var entity in entities)
            {
                /*
                 * We need to iterate through all moving sounds, finally creating
                 * or updating the sounds depending on distance.
                 */
                bool hasBoomSound = entity.Has<components.BoomSound>();
                var cMovingSound = entity.Get<engine.audio.components.MovingSound>();
                components.BoomSound cBoomSound;

                if (hasBoomSound)
                {
                    cBoomSound = entity.Get<components.BoomSound>();
                    Boom.Sound bSound = cBoomSound.Sound;

                    /*
                     * Caution: If the bSound is null, the sound still is loading.
                     * We neither can modify or unload it.
                     */
                    if (bSound == null)
                    {
                        continue;
                    }

                    if (cMovingSound.MotionVolume < minAudibleUShort)
                    {
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
                        bSound.Volume = Math.Max(resultingVolume * 10f, 0.5f);
                        bSound.Pan = resultingPan;
                        bSound.Speed = resultingPitch;
                    }
                }
                else
                {
                    if (cMovingSound.MotionVolume >= minAudibleUShort)
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

        private void _audioThread()
        {
            while (_engine.IsRunning())
            {
                const float timeSlice = 0.1f;
                float usedTime = _audioWorkerQueue.RunPart(timeSlice);
                float sleepTime = Math.Max(0.01f, timeSlice - usedTime);
                Thread.Sleep((int)(sleepTime*1000f));
            }
        }


        public UpdateMovingSoundSystem(
            engine.Engine engine 
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            Thread audioThread = new Thread(_audioThread);
            audioThread.Start();
        }
    }
}
