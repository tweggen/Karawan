using System;
using static engine.Logger;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using Silk.NET.OpenAL;

namespace Boom.OpenAL.systems;

class SoundEntry
{
    public float Distance;
    public DefaultEcs.Entity Entity;
    public ISound? AudioSource;
    public Vector3 Velocity;
    public Vector3 Position;
    public bool Dead = false;
    public bool New = true;
    public engine.audio.components.MovingSound CMovingSound;
}

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
    private Vector3 _cameraPosition;
    private float[] _arrFloatOrientation = new float[6];

    private int _maxSounds = 32;
    private List<SoundEntry> _listSoundEntries = new();
    private Dictionary<DefaultEcs.Entity, SoundEntry> _mapSoundEntries = new();

    // private Queue<AudioSource> _queueUnloadEntries = new();

    public float MinAudibleVolume { get; set; } = 0.005f;

    /**
     * Schedule a sound entry for later deletion in the engine.
     * It may or may not be reused.
     */
    private void _queueUnloadSoundEntry(ISound? audioSource)
    {
        if (null == audioSource)
        {
            /*
             * If the audio source is null here, it might still be loading.
             * However, we shouldn't have been trigeered.
             */
            Error("Tried to remove null sound source.");
            return;
        }
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
    private void _queueLoadMovingSoundToEntity(SoundEntry se)
    {
        DefaultEcs.Entity entity = se.Entity;
        lock (_lo)
        {
            _audioWorkerQueue.Enqueue(() =>
            {
                try
                {
                    se.AudioSource = _api.CreateAudioSource(se.CMovingSound.Sound.Url);
                    _engine.QueueMainThreadAction(() =>
                    {
                        if (!entity.IsAlive)
                        {
                            Trace($"Entity {entity} was not alive any more but queued for loading.");
                            if (_mapSoundEntries.TryGetValue(entity, out var se))
                            {
                                se.Dead = true;
                            }
                            return;
                        }
                        if (!entity.Has<components.BoomSound>())
                        {
                            Error($"Didn't have BoomSound but was queued.");
                            return;
                        }

                        se.AudioSource.Position = se.Position;
                        se.AudioSource.Velocity = se.Velocity;
                        se.AudioSource.IsLooped = se.CMovingSound.Sound.IsLooped;
                        se.AudioSource.Volume = se.CMovingSound.Sound.Volume;
                        se.AudioSource.Speed = se.CMovingSound.Sound.Pitch;
                        entity.Get<components.BoomSound>().AudioSource = se.AudioSource;
                        
                        lock (_lo)
                        {
                            _audioWorkerQueue.Enqueue(() =>
                            {
                                se.AudioSource.Play();
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
        _cameraPosition = _cameraMatrix.Translation;
        
        _api.AL.SetListenerProperty(ListenerVector3.Position, Vector3.Zero);
        _api.AL.SetListenerProperty(ListenerVector3.Velocity, _cameraVelocity);
        
        var vFront = new Vector3(-_cameraMatrix.M31, -_cameraMatrix.M32, -_cameraMatrix.M33);
        var vUp = new Vector3(_cameraMatrix.M21, _cameraMatrix.M22, _cameraMatrix.M23);
        // var vRight = new Vector3(_cameraMatrix.M11, _cameraMatrix.M12, _cameraMatrix.M13);

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
        var vCameraPosition = _cameraPosition;
        
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
            var vPosition = mTransformWorld.Translation - vCameraPosition;
            var vVelocity = entity.Get<engine.joyce.components.Motion>().Velocity;
            var distance = vPosition.Length();

            /*
             * We ignore the doppler data computed in MovingSound and
             * use the implementation of OpenAL.
             */
            if (hasBoomSound)
            {
                cBoomSound = entity.Get<components.BoomSound>();
                ISound? audioSource = cBoomSound.AudioSource;

                SoundEntry? se;
                if (!_mapSoundEntries.TryGetValue(entity, out se))
                {
                    Error($"We don't know anything about entity {entity}.");
                    /*
                     * If we don't have it in the list, just remove the entity and cry.
                     * Be defensice.
                     */
                    if (entity.IsAlive)
                    {
                        entity.Remove<components.BoomSound>();
                    }

                    continue;
                }
                
                /*
                 * We should have the boom sound in our map.
                 * Might be a null audiosource though.
                 */

                /*
                 * Caution: If the bSound is null, the sound still is loading.
                 * We neither can modify or unload it.
                 */
                if (audioSource == null)
                {
                    continue;
                }

                if (cMovingSound.MaxDistance < distance)
                {
                    /*
                     * We do not want to hear that anymore due to the computed
                     * volume level threshold.
                     */
                    se.Dead = true;
                }
                else
                {
                    /*
                     * The sound still is audible, and it had a sound. It should be part
                     * of our data structures. 
                     */

                    /*
                     * Update distance position etc. .
                     */

                    se.Position = vPosition;
                    se.Velocity = vVelocity;
                    se.Distance = distance;
                }
            }
            else
            {
                /*
                 * We did not have a boom sound but might want to have one. Add it to the sound
                 * entries, but do not physically trigger the loading yet.
                 */
                if (cMovingSound.MaxDistance > distance)
                {
                    SoundEntry se = new();
                    se.Entity = entity;
                    se.AudioSource = null;

                    se.Position = vPosition;
                    se.Velocity = vVelocity;
                    se.Distance = vPosition.Length();
                    
                    se.New = true;
                    se.CMovingSound = cMovingSound;

                    _mapSoundEntries.Add(entity, se);
                    _listSoundEntries.Add(se);
                    entity.Set(new components.BoomSound(null));
                }

            }
        }
        
        /*
         * Now map/list sound entries contain all the sound entries we hear, would like to load,
         * including those we do not want to hear (in listRemoveSounds),
         * including those too much.
         *
         * The ones that are not audible any more already are marked as dead.
         *
         * Order the list by distance, then mark everything after the first _maxSounds non-dead
         * sounds dead.
         *
         * Finally, remove the dead sounds.
         */
        _listSoundEntries.Sort((SoundEntry se1, SoundEntry se2) =>
        {
            if (se1.Distance > se2.Distance)
            {
                return 1;
            }
            else
            {
                if (se1.Distance == se2.Distance)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
        });
        
        
        /*
         * Now iterate through the (remaining) list of audible sounds and update them.
         * If they are new, trigger loading them, otherwise update them.
         */
        int maxSounds = 16;
        int currSound = 0;
        float maxDistance = 0f;
        float minDistance = 100000000f;
        foreach (var se in _listSoundEntries)
        {
            if (se.Dead)
            {
                /*
                 * Already is dead, ignore.
                 */
            }
            else
            {
                if (currSound < maxSounds)
                {
                    maxDistance = Single.Max(se.Distance, maxDistance);
                    minDistance = Single.Min(se.Distance, minDistance);
                    
                    /*
                     * If it is new, trigger loading, otherwise modify.
                     */
                    if (se.New)
                    {
                        se.New = false;
                        _queueLoadMovingSoundToEntity(se);
                    }
                    else
                    {
                        if (se.AudioSource != null)
                        {
                            se.AudioSource.Velocity = se.Velocity;
                            se.AudioSource.Position = se.Position;
                        }
                        else
                        {
                            /*
                             * This one still is loading.
                             */
                        }
                    }

                    /*
                     * we still can keep the sound.
                     */
                    ++currSound;
                }
                else
                {
                    se.Dead = true;
                }
            }
        }
        
        /*
         * Now there's a couple of dead sounds we want to remove. They already are marked as dead.
         */
        {
            List<SoundEntry> listRemoveSounds = new();
            foreach (var se in _listSoundEntries)
            {
                if (se.Dead && !se.New)
                {
                    listRemoveSounds.Add(se);
                }
            }

            /*
             * First, remove all the sound entries from the sound map, thereby queueing unload.
             */
            foreach (var deadse in listRemoveSounds)
            {
                DefaultEcs.Entity entity = deadse.Entity;
                _mapSoundEntries.Remove(deadse.Entity);
                if (entity.IsAlive)
                {
                    entity.Remove<components.BoomSound>();
                }

                _queueUnloadSoundEntry(deadse.AudioSource);
            }
        }
        _listSoundEntries.RemoveAll(se => se.Dead);
        // Trace( $"Keeping {_listSoundEntries.Count} sounds at maximum distance {maxDistance}m, minimum distance {minDistance} cameraPos {vCameraPosition}.");
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

