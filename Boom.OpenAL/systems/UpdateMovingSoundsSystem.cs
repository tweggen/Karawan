using System;
using static engine.Logger;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using Silk.NET.OpenAL;

namespace Boom.OpenAL.systems;

/**
 * Note: The only transition that does not happen inside the main thread
 * is the loading of the audio source.
 *
 * To keep it maintainable, we keep changing the data structure in the
 * main thread.
 */
class SoundEntry
{
    public float Distance;
    public DefaultEcs.Entity Entity;
    public ISound? AudioSource;
    public Vector3 Velocity;
    public Vector3 Position;
    public bool Dead = false;
    public bool New = true;
    public bool IsUnloaded = false;
    // public bool LostEntity = false;
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
    private DefaultEcs.Entity _playerEntity;

    private Matrix4x4 _cameraMatrix;
    private Vector3 _cameraVelocity;
    private Vector3 _cameraPosition;

    private Matrix4x4 _playerMatrix;
    private Vector3 _playerVelocity;
    private Vector3 _playerPosition;

    private readonly float[] _arrFloatOrientation = new float[6];

    // private int _maxSounds = 32;
    private List<SoundEntry> _listSoundEntries = new();
    private Dictionary<DefaultEcs.Entity, SoundEntry> _mapSoundEntries = new();

    // private Queue<AudioSource> _queueUnloadEntries = new();

    public float MinAudibleVolume { get; set; } = 0.005f;

    
    /**
     * Schedule a sound entry for later deletion in the engine.
     * It may or may not be reused.
     */
    private void _queueUnloadSoundEntry(ISound audioSource)
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
            // Trace($"Triggering unload.");
            _audioWorkerQueue.Enqueue(() =>
            {
//                 Trace($"Unloading.");
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
                    ISound audioSource;
                    audioSource = _api.CreateAudioSource(se.CMovingSound.Sound.Url);

                    // TXWTODO: What to do if loading the audio source was not successful?
                    
                    _engine.QueueMainThreadAction(() =>
                    {
                        /*
                         * It may be that an entity is not alive any more, e.g. if its fragment had
                         * been disposed.
                         *
                         * In that case we need to remove it from our lists, plus, the audio source
                         * can be disposed again.
                         */

                        bool cleanupSoundEntry = false;

                        if (!entity.IsAlive)
                        {
                            // Trace("Entity {entity} dead");
                            cleanupSoundEntry = true;
                        }

                        if (null == audioSource)
                        {
                            // Trace("Unable to load");
                            cleanupSoundEntry = true;
                        }

                        if (se.Dead)
                        {
                            //  Trace("Already tagged dead.");
                            cleanupSoundEntry = true;
                        }
                        
                        /*
                         * If
                         * - the sound entry could not be loaded
                         * - or the entity wasnot alive
                         * - or it is dead by now
                         * we must remove the sound entry again.
                         */
                        if (cleanupSoundEntry)
                        {
                            // Trace($"Entity {entity} was not alive any more but queued for loading.");
                            
                            se.Dead = true;
                            se.IsUnloaded = true;

                            if (null != audioSource)
                            {
                                /*
                                 * Then immediately delete the audio source again that was
                                 * setup for me.
                                 */
                                audioSource.Dispose();
                            }
                            
                            return;
                        }

                        /*
                         * Finally, write the audiosource to the sound entry and create
                         * the boom entry for cleanup with the entity.
                         */
                        se.AudioSource = audioSource;
#if false
                        if (entity.Has<components.BoomSound>())
                        {
                            Error($"Queued Entity {entity} already had BoomSound.");
                            return;
                        }
#endif

                        /*
                         * audioSource must be non-null here.
                         */
                        // entity.Set(new components.BoomSound(audioSource));
                        audioSource.Position = se.Position;
                        audioSource.Velocity = se.Velocity;
                        audioSource.IsLooped = se.CMovingSound.Sound.IsLooped;
                        audioSource.Volume = se.CMovingSound.Sound.Volume;
                        audioSource.Speed = se.CMovingSound.Sound.Pitch;

                        audioSource.Play();
                        // Trace($"Loading entity {entity} _nMovingSounds = {_nMovingSounds}");
                        lock (_lo)
                        {
                            ++_nMovingSounds;
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
        
        
        _api.AL.SetListenerProperty(ListenerVector3.Position, Vector3.Zero);
        _api.AL.SetListenerProperty(ListenerVector3.Velocity, _cameraVelocity);
        
        /*
         * We use the direction from the camera but the position of the ship.
         */
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
            //bool hasBoomSound = entity.Has<components.BoomSound>();
            
            /*
             * Look, if we already have a record for this sound.
             */
            bool isInMap = _mapSoundEntries.TryGetValue(entity, out SoundEntry seFound);
            
            var cMovingSound = entity.Get<engine.audio.components.MovingSound>();
            
            var mTransformWorld = entity.Get<engine.transform.components.Transform3ToWorld>().Matrix;
            var vPosition = mTransformWorld.Translation - vCameraPosition;
            var vVelocity = entity.Get<engine.joyce.components.Motion>().Velocity;
            var distance = vPosition.Length();

            /*
             * If it has a boom sound, it already is completely loaded.
             */
            if (isInMap)
            {
                ISound audioSource = seFound.AudioSource;

                /*
                 * We should have the boom sound in our map.
                 * Might be a null audiosource though.
                 * However, this doesn't matter, as we do not apply the
                 * values to the audiosource at this point.
                 */

                if (cMovingSound.MaxDistance < distance)
                {
                    /*
                     * We do not want to hear that anymore due to the computed
                     * volume level threshold.
                     */
                    seFound.Dead = true;
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

                    seFound.Position = vPosition;
                    seFound.Velocity = vVelocity;
                    seFound.Distance = distance;
                }
            }
            else
            {
                /*
                 * We did not have a boom sound but might want to have one. Add it to the sound
                 * entries, but do not physically trigger the loading yet.
                 */
                if (cMovingSound.MaxDistance >= distance)
                {
                    // Trace($"New SoundEntry for entity {entity}.");
                    SoundEntry seNew = new();
                    seNew.Entity = entity;
                    seNew.AudioSource = null;

                    seNew.Position = vPosition;
                    seNew.Velocity = vVelocity;
                    seNew.Distance = vPosition.Length();
                    
                    seNew.New = true;
                    seNew.CMovingSound = cMovingSound;

                    _mapSoundEntries.Add(entity, seNew);
                    _listSoundEntries.Add(seNew);
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
                continue;
            }

            /*
             * Entity vanished? Then remove me.
             */
            if (!se.Entity.IsAlive)
            {
                // Trace($"Killing entity {se.Entity}.");
                se.Dead = true;
                continue;
            }
            
            /*
             * Entity is alive. Shall hear the sound.
             */
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
                         * This one still is loading, unloaded can't be true.
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
        
        /*
         * Now there's a couple of dead sounds we want to remove. They already are marked as dead.
         */
        {
            /*
             * Collect all dead entries. Note, that these can be entries which
             * - are newly found, but immediately outnumbered by distance, loading not triggerred yet
             * - loading just has been triggered, it is not finished yet.
             */
            List<SoundEntry> listRemoveSounds = new();
            foreach (var se in _listSoundEntries)
            {
                if (se.Dead)
                {
                    listRemoveSounds.Add(se);
                }
            }

            /*
             * Remove all of the dead entries also from the list.
             */
            _listSoundEntries.RemoveAll(se => se.Dead);

            /*
             * Finally, remove all the sound entries from the sound map, thereby queueing unload.
             *
             * - the dead entries are removed from the map
             * - if the entity associated with the entry is alive, the BoomSound component
             *   is removed.
             * - if an audio source already has been loaded for this entry,
             *   it is dissociated, unloading is triggered.
             *
             * => Immediately, starting with the next frame, a load for this entity
             *    could be triggered again.
             */
            foreach (var deadse in listRemoveSounds)
            {
                DefaultEcs.Entity entity = deadse.Entity;
                _mapSoundEntries.Remove(deadse.Entity);
                if (entity.IsAlive)
                {
                    // Trace($"Removing boomSound from {entity}.");
                    // entity.Remove<components.BoomSound>();
                }
                else
                {
                    /*
                     * It can't be an entity that is not alive at this point.
                     * Eventually, we collect only alive entries in the system,
                     * and nobody can kill it on the way.
                     */
                    Error($"I don't understand, why entity {entity} is dead at this point.");
                }

#if true
                /*
                 * If the audio source already loaded successfully, unload it here.
                 */
                if (deadse.AudioSource != null)
                {
                    ISound seWannaUnload = deadse.AudioSource;
                    deadse.AudioSource = null;
                    deadse.IsUnloaded = true;
                    _queueUnloadSoundEntry(seWannaUnload);
                }
#endif
            }
        }
        
        // Trace( $"Keeping {_listSoundEntries.Count} sounds at maximum distance {maxDistance}m, minimum distance {minDistance} cameraPos {vCameraPosition}.");
    }


    private void _readCameraValues()
    {
        DefaultEcs.Entity eCamera;
        DefaultEcs.Entity ePlayer;
        lock (_lo)
        {
            eCamera = _cameraEntity;
            ePlayer = _playerEntity;
            if (!eCamera.IsAlive || !ePlayer.IsAlive)
            {
                return;
            }
            try {
                _cameraMatrix = eCamera.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                _cameraVelocity = eCamera.Get<engine.joyce.components.Motion>().Velocity;
                _cameraPosition = _cameraMatrix.Translation;

                _playerMatrix = ePlayer.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                _playerVelocity = ePlayer.Get<engine.joyce.components.Motion>().Velocity;
                _playerPosition = _playerMatrix.Translation;
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
             * We do not update the AL listener, instead we assume the listener to be
             * at the origin. Instead, we wait for everything else to update.
             */
        }
    }


    private void _onPlayerEntityChanged(object? sender, DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_playerEntity != entity)
            {
                _playerEntity = entity;
                isChanged = true;
            }
        }

        if (isChanged)
        {
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
        _engine.OnCameraEntityChanged += _onCameraEntityChanged;
        _engine.OnPlayerEntityChanged += _onPlayerEntityChanged;
        _onCameraEntityChanged(_engine, _engine.GetCameraEntity());
        _onPlayerEntityChanged(_engine, _engine.GetPlayerEntity());
    }
}

