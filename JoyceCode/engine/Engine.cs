﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using DefaultEcs;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace engine
{

    class EntitySetupAction
    {
        public string EntityName;
        public  Action<DefaultEcs.Entity> SetupAction;
    }
    
    public class Engine
    {
        private object _lo = new();

        private int _nextId = 0;

        private DefaultEcs.World _ecsWorld;
        private DefaultEcs.Command.EntityCommandRecorder _entityCommandRecorder;
        private List<IList<DefaultEcs.Entity>> _listDoomedEntityLists = new();

        private IPlatform _platform;

        private hierarchy.API _aHierarchy;
        private transform.API _aTransform;
        private physics.API _aPhysics;

        private behave.systems.BehaviorSystem _systemBehave;
        private physics.systems.ApplyPosesSystem _systemApplyPoses;
        private physics.systems.MoveKineticsSystem _systemMoveKinetics;
        private audio.systems.MovingSoundsSystem _systemMovingSounds;

        private SortedDictionary<float, IPart> _dictParts;
        
        private readonly Queue<EntitySetupAction> _queueEntitySetupActions = new();
        // private readonly Queue<Action> = new();
        
        private Thread _logicalThread;
        private readonly Stopwatch _queueStopwatch = new();

        private readonly WorkerQueue _workerCleanupActions = new("engine.Engine.Cleanup");
        private readonly WorkerQueue _workerMainThreadActions = new("engine.Engine.MainThread");

        private bool _mayCallLogical = false;

        private bool _isFullscreen = false;
        
        private physics.Manager _managerPhysics;
        public readonly SceneSequencer SceneSequencer;        
        
        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<string> KeyPress;
        public event EventHandler<string> KeyRelease;
        public event EventHandler<Vector2> OnTouchPress;
        public event EventHandler<Vector2> OnTouchRelease;
        
        private Entity _cameraEntity;
        public event EventHandler<DefaultEcs.Entity> OnCameraEntityChanged;
        private Entity _playerEntity;
        public event EventHandler<DefaultEcs.Entity> OnPlayerEntityChanged;

        

        private builtin.tools.RunningAverageComputer _fpsCounter = new();

        
        public event EventHandler<physics.ContactInfo> OnContactInfo {
            add => _aPhysics.OnContactInfo += value; remove => _aPhysics.OnContactInfo -= value;
        } 

        
        public BepuPhysics.Simulation Simulation
        {
            get => _aPhysics.Simulation;
        }

        public enum EngineState {
            Initialized,
            Starting,
            Running,
            Stopping,
            Stopped
        };

        
        public EngineState State { get; private set; }
        public event EventHandler<EngineState> EngineStateChanged;

        public void SetEngineState( in EngineState newState )
        {
            bool isChanged = false;
            lock(_lo)
            {
                if (newState != State)
                {
                    State = newState;
                    isChanged = true;
                }
            }
            if (isChanged)
            {
                EngineStateChanged.Invoke(this, newState);
            }
        }


        public Entity GetCameraEntity()
        {
            lock (_lo)
            {
                return _cameraEntity;
            }
        }


        public Entity GetPlayerEntity()
        {
            lock (_lo)
            {
                return _playerEntity;
            }
        }


        public void SetCameraEntity(in DefaultEcs.Entity entity)
        {
            bool entityChanged = false;
            lock (_lo)
            {
                if (_cameraEntity != entity)
                {
                    entityChanged = true;
                    _cameraEntity = entity;
                }
            }

            if (entityChanged)
            {
                OnCameraEntityChanged?.Invoke(this, entity);
            }
        }

        
        public void SetPlayerEntity(in DefaultEcs.Entity entity)
        {
            bool entityChanged = false;
            lock (_lo)
            {
                if (_playerEntity != entity)
                {
                    entityChanged = true;
                    _playerEntity = entity;
                }
            }

            if (entityChanged)
            {
                OnPlayerEntityChanged?.Invoke(this, entity);
            }
        }

        
        public int GetNextId()
        {
            lock(_lo)
            {
                return ++_nextId;
            }
        }
        

        public hierarchy.API GetAHierarchy()
        {
            return _aHierarchy;
        }


        public transform.API GetATransform()
        {
            return _aTransform;
        }


        public physics.API GetAPhysics()
        {
            return _aPhysics;
        }

        
        
        public DefaultEcs.World GetEcsWorld()
        {
            return _ecsWorld;
        }

        
        public DefaultEcs.Command.WorldRecord GetEcsWorldRecord()
        {
            return _entityCommandRecorder.Record(_ecsWorld);
        }

        
        public void ApplyEcsRecorder(in DefaultEcs.Command.EntityCommandRecorder recorder)
        {
            recorder.Execute();
        }
        

        /**
         * Called by platform as soon platform believes the timeline starts.
         */
        public void StartTimeline()
        {
            lock (_lo)
            {
                _mayCallLogical = true;
            }
        }
        
        
        private void _commitWorldRecord()
        {
            _entityCommandRecorder.Execute();
        }

        
        private void _executeDoomedEntities()
        {
            List<IList<DefaultEcs.Entity>> listList;
            lock (_lo)
            {
                if (_listDoomedEntityLists.Count == 0) 
                { 
                    return;
                }
                listList = _listDoomedEntityLists;
                _listDoomedEntityLists = new();
            }
            if( null==listList )
            {
                return;
            }
            foreach(var list in listList)
            {
                foreach(var entity in list)
                {
                    entity.Dispose();
                }
            }
        }


        public void AddDoomedEntities(in IList<DefaultEcs.Entity> listDoomedEntities)
        {
            lock (_lo)
            {
                _listDoomedEntityLists.Add(listDoomedEntities);
            }
        }


        public void AddDoomedEntity(DefaultEcs.Entity entity)
        {
            lock (_lo)
            {
                List<Entity> listEntity = new List<Entity>();
                listEntity.Add(entity);
                _listDoomedEntityLists.Add(listEntity);
            }
        }


        public DefaultEcs.Entity CreateEntity(string name)
        {
            DefaultEcs.Entity entity = _ecsWorld.CreateEntity();
            entity.Set(new joyce.components.EntityName(name));
            return entity;
        }


        private void _executeEntitySetupActions(float matTime)
        {
            _queueStopwatch.Reset();
            _queueStopwatch.Start();
            while(_queueStopwatch.Elapsed.TotalMilliseconds < matTime*1000f)
            {
                EntitySetupAction entitySetupAction;
                lock (_lo)
                {
                    if( _queueEntitySetupActions.Count==0)
                    {
                        break;
                    }
                    entitySetupAction = _queueEntitySetupActions.Dequeue();
                }

                DefaultEcs.Entity entity = CreateEntity(entitySetupAction.EntityName);
                try {                    
                    entitySetupAction.SetupAction(entity);
                } catch( Exception e )
                {
                    Warning($"Error executing entity setup action: {e}.");
                    entity.Dispose();
                }
            }
            _queueStopwatch.Stop();

            int queueLeft;
            lock (_lo)
            {
                queueLeft = _queueEntitySetupActions.Count;
            }

            if (0 < queueLeft)
            {
                Trace( $"Left {queueLeft} items in setup actions queue.");
            }

        }


        public void QueueEntitySetupAction(
            string entityName, Action<DefaultEcs.Entity> setupAction)
        {
            lock (_lo)
            {
                _queueEntitySetupActions.Enqueue(
                    new EntitySetupAction
                    {
                        EntityName = entityName,
                        SetupAction = setupAction
                    }
                );
            }
        }


        public void QueueCleanupAction(Action action)
        {
            _workerCleanupActions.Enqueue(action);
        }

        public void QueueMainThreadAction(Action action)
        {
            _workerMainThreadActions.Enqueue(action);
        }


        public void AddContactListener(DefaultEcs.Entity entity)
        {
            _aPhysics.AddContactListener(entity);
        }

        public void RemoveContactListener(DefaultEcs.Entity entity)
        {
            _aPhysics.RemoveContactListener(entity);
        }


        /**
         * Called by the platform on a new physical frame.
         */
        public void OnPhysicalFrame(float dt)
        {
            PhysicalFrame?.Invoke(this, dt);
            
            /*
             * Compute a running average of fps.
             */
            _fpsCounter.Add(dt);
        }
        
        private bool _firstTime = true;

        /**
         * Control, which camera is the source for audio information.
         * This takes the position and direction of the first camera
         * that is found to be active.
         */
        private uint _audioCameraMask = 0xffffffff;

        private void _onLogicalFrame(float dt)
        {
            EngineState engineState;
            lock (_lo)
            {
                engineState = State;
            }

            /*
             * If the engine is stopped, do not do any logical frame stuff.
             */
            if (engineState == EngineState.Stopped)
            {
                return;
            }

            /*
             * Before rendering the first time and calling user handlers the first time,
             * we need to read physics to transforms, update hierarchy and transforms.
             * That way user handlers have the transform2world available.
             */
            if( _firstTime )
            {
                _firstTime = false;

                /*
                 * Apply poses needs input from simulation
                 */
                _systemApplyPoses.Update(dt);

                /*
                 * hierarchy needs
                 * - input from user handlers
                 */
                _aHierarchy.Update();

                /*
                 * transform system needs
                 * - updated hierarchy system
                 * - input from user handlers
                 * - input from physics
                 */
                _aTransform.Update();

                /*
                 * Move kinetics re quires 
                 * - input from user, already processed by Transform System
                 */
                _systemMoveKinetics.Update(dt);
            }

            /*
             * Goal: shortest latency from user input to screen.
             */

            /*
             * Call the various ways of user behavior and/or controllers.
             * They will read world position and modify physics and or positions
             * 
             * Require: Previously computed world transforms.
             */
            _systemBehave.Update(dt);

            if (_mayCallLogical)
            {
                LogicalFrame?.Invoke(this, dt);
            }

            /*
             * After everything has behaved, read the camera(s) to get
             * the camera positions for further processing.
             */
            var vCameraPosition = new Vector3(0f, 0f, 0f);
            var vCameraRight = new Vector3(1f, 1f, 0f);
            var listCameras = GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eCamera in listCameras)
            {
                var cCamera3 = eCamera.Get<engine.joyce.components.Camera3>();
                var cTransform3ToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>();
                var mToWorld = cTransform3ToWorld.Matrix;
                
                vCameraPosition = cTransform3ToWorld.Matrix.Translation;
                vCameraRight = new Vector3(mToWorld.M11, mToWorld.M12, mToWorld.M13);

                _systemMovingSounds.SetListenerPosRight(vCameraPosition, vCameraRight);
                
                break;
            }

            /*
             * We can update moving sounds only after the behaviour has defined
             * the velocities.
             */
            _systemMovingSounds.Update(dt);


            /*
             * Advance physics, based on new user input and/or gravitation.
             */
            _aPhysics.Update(dt);

            /*
             * Apply poses needs input from simulation
             */
            _systemApplyPoses.Update(dt);

            /*
             * hierarchy needs
             * - input from user handlers
             */
            _aHierarchy.Update();

            /*
             * transform system needs
             * - updated hierarchy system
             * - input from user handlers
             * - input from physics
             */
            _aTransform.Update();

            /*
             * Move kinetics requires 
             * - input from user, already processed by Transform System
             */
            _systemMoveKinetics.Update(dt);

            /*
             * Write back all entity modifications to the objects.
             */
            _commitWorldRecord();

            /*
             * If no new frame has been created, read all geom entities for rendering
             * into data structures.
             */
            _platform.CollectRenderData();

            // TXWTODO: Measure the time of all actions.
            /*
             * Async delete any entities that shall be deleted 
             */
            _executeDoomedEntities();
            
            
            /*
             * Async create / setup new entities.
             */
            _executeEntitySetupActions(0.001f);
            _workerMainThreadActions.RunPart(0.001f);
            _workerCleanupActions.RunPart(0.001f);
        }


        private double _timeLeft;
        private int _fpsLogical = 60;


        public void AddPart(float zOrder, in IScene scene0, in IPart part0)
        {
            _dictParts.Add(zOrder, part0);
        }


        public void RemovePart(in IPart part)
        {
            foreach (KeyValuePair<float, IPart> kvp in _dictParts)
            {
                if (kvp.Value == part)
                {
                    _dictParts.Remove(kvp.Key);
                    return;
                }
            }
        }


        public void GetControllerState(out ControllerState controllerState)
        {
            _platform.GetControllerState(out controllerState);
        }
        

        public void GetMouseMove(out Vector2 vMouseMove)
        {
            _platform.GetMouseMove(out vMouseMove);
        }


        public void TakeKeyPress(string code)
        {
            KeyPress?.Invoke(this, code);
        }


        public void TakeKeyRelease(string code)
        {
            KeyRelease?.Invoke(this, code);
        }


        public void TakeTouchPress(Vector2 position)
        {
            OnTouchPress?.Invoke(this, position);
        }

        public void TakeTouchRelease(Vector2 position)
        {
            OnTouchRelease?.Invoke(this, position);
        }

        public void Execute()
        {
            _platform.Execute();
        }


        public bool IsFullscreen()
        {
            lock (_lo)
            {
                return _isFullscreen;
            }
        }


        public void SetFullscreen(bool isFullscreen)
        {
            IPlatform platform = null;
            lock (_lo)
            {
                _isFullscreen = isFullscreen;
                platform = _platform;
            }

            if (null != platform)
            {
                platform.SetFullscreen(isFullscreen);
            }
        }


        private void _logicalThreadFunction()
        {
            float invFps = 1f / 60f;
            float totalTime = 0f;
            long previousSeconds = 0;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            while (_platform.IsRunning())
            {
                /*
                 * Wait for the next frame or rock it.
                 */
                stopWatch.Stop();
                {
                    float elapsed = (float)stopWatch.Elapsed.TotalSeconds;
                    totalTime += elapsed;
                    // Trace($"elapsed {elapsed} totalTime {totalTime}");
                }
                stopWatch.Reset();

                /*
                 * keep times in range
                 */
                while (totalTime > 1f)
                {
                    totalTime -= 1f;
                }


                if (totalTime < invFps)
                {
                    // Trace("Sleeping.");
                    stopWatch.Start();
                    Thread.Sleep(1);
                    continue;
                }

                stopWatch.Start();
                /*
                 * Run as many logical frames as have been elapsed.
                 */
                while (totalTime > invFps)
                {
                    // Trace( "Calling logical.");
                    _onLogicalFrame(invFps);
                    totalTime -= invFps;
                }
                
                stopWatch.Stop();
                float processedTime = (float)stopWatch.Elapsed.TotalSeconds;
                stopWatch.Reset();

                totalTime += processedTime;

                /*
                 * Now, depending on the remaining time, sleep a bit.
                 */
                stopWatch.Start();

                /*
                 * Do these updates every second
                 */
                {
                    long seconds = Stopwatch.GetTimestamp() / Stopwatch.Frequency;
                    if (previousSeconds != seconds)
                    {
                        float dt = _fpsCounter.GetRunningAverage();
                        float fps = 0f;
                        if (0 == dt)
                        {
                            fps = 0f;
                        }
                        else
                        {
                            fps = 1f / dt;
                        }

                        Trace($"#fps {fps}");
                    }
                    previousSeconds = seconds;
                }
            }

        }

        /**
         * Call after all dependencies are set.
         */
        public void SetupDone()
        {
            _aHierarchy = new hierarchy.API(this);
            _aTransform = new transform.API(this);
            _aPhysics = new physics.API(this);
            _systemBehave = new(this);
            _systemApplyPoses = new(this);
            _systemMoveKinetics = new(this);
            _systemMovingSounds = new(this);
            _managerPhysics = new physics.Manager();
            _managerPhysics.Manage(this);

            _logicalThread = new Thread(_logicalThreadFunction);
            _logicalThread.Priority = ThreadPriority.AboveNormal;
        }


        public bool IsRunning()
        {
            return _platform.IsRunning();
        }
        

        public void PlatformSetupDone()
        {
            {
                /*
                 * Zero out initial accumulated mouse move.
                 */
                GetMouseMove(out _);
            }

            /*
             * Start the reality as soon the platform also is set up.
             */
            _logicalThread.Start();
        }
        

        public Engine( engine.IPlatform platform )
        {
            _nextId = 0;
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
            _entityCommandRecorder = new(4096, 1024*1024);
            _dictParts = new();
            SceneSequencer = new(this);
        }
    }
}
 