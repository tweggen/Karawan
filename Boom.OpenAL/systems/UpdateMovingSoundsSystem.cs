using System;
using static engine.Logger;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenAL;

namespace Boom.OpenAL.systems;


#if true
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
    private DefaultEcs.Entity _cameraEntity;

    private Matrix4x4 _cameraMatrix;
    private Vector3 _cameraVelocity;
    private float[] _arrFloatOrientation = new float[6];
    
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
        engine.audio.components.MovingSound cMovingSound,
        Vector3 vPosition,
        Vector3 vVelocity)
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

                        audioSource.Position = vPosition;
                        audioSource.Velocity = vVelocity;
                        
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
    

    protected override unsafe void PreUpdate(float dt)
    {
        _readCameraValues();
        
        /*
         * Move listener. Note, that the camera by definition is at 0/0/0
         *
         * That means that at == front, pos == zero.
         */
        var vPos = _cameraMatrix.Translation;
        
        _api.AL.SetListenerProperty(ListenerVector3.Position, Vector3.Zero);
        _api.AL.SetListenerProperty(ListenerVector3.Velocity, _cameraVelocity);
        
        var vFront = new Vector3(-_cameraMatrix.M31, -_cameraMatrix.M32, -_cameraMatrix.M33);
        var vUp = new Vector3(_cameraMatrix.M21, _cameraMatrix.M22, _cameraMatrix.M23);
        // var vRight = new Vector3(_cameraMatrix.M11, _cameraMatrix.M12, _cameraMatrix.M13);

        Trace( $"Front is {vFront}");
        _arrFloatOrientation[0] = vFront.X;
        _arrFloatOrientation[1] = vFront.Y;
        _arrFloatOrientation[2] = vFront.Z;
        _arrFloatOrientation[3] = vUp.X;
        _arrFloatOrientation[4] = vUp.Y;
        _arrFloatOrientation[5] = vUp.Z;

        fixed (float* pO = _arrFloatOrientation)
        {
            _api.AL.SetListenerProperty(ListenerFloatArray.Orientation, pO);
        }
    }
    

    protected override void PostUpdate(float dt)
    {
    }
    

    protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        var al = _api.AL;
        var vCameraPosition = _cameraMatrix.Translation;

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
            
            var mTransformWorld = entity.Get<engine.transform.components.Transform3ToWorld>().Matrix;
            var vVelocity = entity.Get<engine.joyce.components.Motion>().Velocity - _cameraVelocity;
            var vPosition = mTransformWorld.Translation - vCameraPosition;

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
                     * We ignoere the doppler data computed in MovingSound and
                     * use the implementation of OpenAL.
                     */
                    audioSource.Position = vPosition;
                    audioSource.Velocity = vVelocity;
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
                    _queueLoadMovingSoundToEntity(entity, cMovingSound, vPosition, vVelocity);
                }

            }
        }
    }


    private void _readCameraValues()
    {
        DefaultEcs.Entity eCamera;
        lock (_lo)
        {
            eCamera = _cameraEntity;
            if (!eCamera.IsAlive)
            {
                return;
            }
            try {
                _cameraMatrix = eCamera.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                _cameraVelocity = eCamera.Get<engine.joyce.components.Motion>().Velocity;
            }
            catch (Exception e)
            {
                Error($"Camera entity {eCamera} does not of both Transform3ToWorld and Motion set.");
            }
        }
        /*
         * Now we can use the values during the update cycle.
         */
    }
    

    private void _onCameraEntityChanged(object? sender, DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_cameraEntity != entity)
            {
                _cameraEntity = entity;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            /*
             * Read camera velocity, position and direction.
             */
            _readCameraValues();

            /*
             * We do not update the AL listener, instead we assume the listener to be
             * at the origin. Instead, we wait for everything else to update.
             */
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
        _engine.CameraEntityChanged += _onCameraEntityChanged;
        _onCameraEntityChanged(_engine, _engine.GetCameraEntity());
    }
}

#else

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