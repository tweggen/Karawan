using System;
using static engine.Logger;
using System.Collections.Generic;

namespace Boom.OpenAL.systems;


#if false
[DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
[DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(engine.joyce.components.Motion))]
sealed public class UpdateMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private object _lo = new();
    private engine.Engine _engine;
    private engine.WorkerQueue _audioWorkerQueue = new("UpdateMovingSoundSystem.Audio");
    private Boom.OpenAL.API _api;
    private int _nMovingSounds = 0;
    
    // private Queue<AudioSource> _queueUnloadEntries = new();

    public float MinAudibleVolume { get; set; } = 0.005f;

    /**
     * Schedule a sound entry for later deletion in the engine.
     * It may or may not be reused.
     */
    private void _queueUnloadSoundEntry(AudioSource audioSource)
    {
        lock (_lo)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                audioSource.Stop();
                audioSource.Dispose();
                lock (_lo)
                {
                    --_nMovingSounds;
                }
            });
        }
    }

    /**
     * Queue obtaining a sound and attaching it to this entity.
     */
    private void _queueLoadMovingSoundToEntity(
        DefaultEcs.Entity entity,
        engine.audio.components.MovingSound cMovingSound)
    {
        lock (_lo)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                try
                {
                    AudioSource audioSource = _api.CreateAudioSource(cMovingSound.Sound.Url);
                    _engine.QueueMainThreadAction(() =>
                    {
                        entity.Set(new components.BoomSound(audioSource));

                        audioSource.Volume = cMovingSound.Sound.Volume * cMovingSound.MotionVolume;
                        audioSource.Pan = cMovingSound.MotionPan;
                        audioSource.Speed = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;

                        lock (_lo)
                        {
                            _audioWorkerQueue.Enqueue(() =>
                            {
                                audioSource.Play();
                                lock (_lo)
                                {
                                    Trace($"_nMovingSounds = {_nMovingSounds}");
                                    ++_nMovingSounds;
                                }
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Ignore and just don't create sound.
                }
            });
        }
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
                AudioSource audioSource = cBoomSound.AudioSource;

                /*
                 * Caution: If the bSound is null, the sound still is loading.
                 * We neither can modify or unload it.
                 */
                if (audioSource == null)
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
                    _queueUnloadSoundEntry(audioSource);
                }
                else
                {
                    /*
                     * We have a sound and can modify it.
                     * Adjust the settings of the raylib sound by the MovingSound data.
                     */
                    float resultingVolume = cMovingSound.Sound.Volume * (float)cMovingSound.MotionVolume / engine.audio.components.MovingSound.MotionVolumeMax;
                    float resultingPitch = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;
                    float resultingPan = (float) cMovingSound.MotionPan / engine.audio.components.MovingSound.MotionPanMax;
                    audioSource.Volume = Math.Min(resultingVolume, 1.0f); 
                        //Math.Min(resultingVolume * 4f, 2.0f);
                    audioSource.Pan = resultingPan;
                    audioSource.Speed = resultingPitch;
                    entity.Get<engine.audio.components.MovingSound>().NFrames = 0;
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
        engine.Engine engine, API api 
    )
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _api = api;
        Thread audioThread = new Thread(_audioThread);
        audioThread.Start();
    }
}
#endif


#if true
[DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
sealed public class UpdateMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private object _lo = new();
    private engine.Engine _engine;
    private engine.WorkerQueue _audioWorkerQueue = new("UpdateMovingSoundSystem.Audio");
    private Boom.OpenAL.API _api;
    private int _nMovingSounds = 0;
    
    // private Queue<AudioSource> _queueUnloadEntries = new();

    public float MinAudibleVolume { get; set; } = 0.005f;

    /**
     * Schedule a sound entry for later deletion in the engine.
     * It may or may not be reused.
     */
    private void _queueUnloadSoundEntry(AudioSource audioSource)
    {
        lock (_lo)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                audioSource.Stop();
                audioSource.Dispose();
                lock (_lo)
                {
                    --_nMovingSounds;
                }
            });
        }
    }

    /**
     * Queue obtaining a sound and attaching it to this entity.
     */
    private void _queueLoadMovingSoundToEntity(
        DefaultEcs.Entity entity,
        engine.audio.components.MovingSound cMovingSound)
    {
        lock (_lo)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                try
                {
                    AudioSource audioSource = _api.CreateAudioSource(cMovingSound.Sound.Url);
                    _engine.QueueMainThreadAction(() =>
                    {
                        entity.Set(new components.BoomSound(audioSource));

                        audioSource.Volume = cMovingSound.Sound.Volume * cMovingSound.MotionVolume;
                        audioSource.Pan = cMovingSound.MotionPan;
                        audioSource.Speed = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;

                        lock (_lo)
                        {
                            _audioWorkerQueue.Enqueue(() =>
                            {
                                audioSource.Play();
                                lock (_lo)
                                {
                                    Trace($"_nMovingSounds = {_nMovingSounds}");
                                    ++_nMovingSounds;
                                }
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Ignore and just don't create sound.
                }
            });
        }
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
                AudioSource audioSource = cBoomSound.AudioSource;

                /*
                 * Caution: If the bSound is null, the sound still is loading.
                 * We neither can modify or unload it.
                 */
                if (audioSource == null)
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
                    _queueUnloadSoundEntry(audioSource);
                }
                else
                {
                    /*
                     * We have a sound and can modify it.
                     * Adjust the settings of the raylib sound by the MovingSound data.
                     */
                    float resultingVolume = cMovingSound.Sound.Volume * (float)cMovingSound.MotionVolume / engine.audio.components.MovingSound.MotionVolumeMax;
                    float resultingPitch = cMovingSound.Sound.Pitch * cMovingSound.MotionPitch;
                    float resultingPan = (float) cMovingSound.MotionPan / engine.audio.components.MovingSound.MotionPanMax;
                    audioSource.Volume = Math.Min(resultingVolume, 1.0f); 
                        //Math.Min(resultingVolume * 4f, 2.0f);
                    audioSource.Pan = resultingPan;
                    audioSource.Speed = resultingPitch;
                    entity.Get<engine.audio.components.MovingSound>().NFrames = 0;
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
        engine.Engine engine, API api 
    )
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _api = api;
        Thread audioThread = new Thread(_audioThread);
        audioThread.Start();
    }
}
#endif