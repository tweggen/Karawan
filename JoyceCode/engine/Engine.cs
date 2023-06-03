using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using DefaultEcs;
using static engine.Logger;

namespace engine
{
    public class Engine
    {
        private object _lo = new();

        private int _nextId = 0;

        private DefaultEcs.World _ecsWorld;
        private DefaultEcs.Command.EntityCommandRecorder _entityCommandRecorder;
        private List<IList<DefaultEcs.Entity>> _listDoomedEntityLists = new();

        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private engine.behave.systems.BehaviorSystem _systemBehave;
        private engine.physics.systems.ApplyPosesSystem _systemApplyPoses;
        private engine.physics.systems.MoveKineticsSystem _systemMoveKinetics;
        private engine.audio.systems.MovingSoundsSystem _systemMovingSounds;

        private SortedDictionary<float, IPart> _dictParts;

        private readonly Queue<Action<DefaultEcs.Entity>> _queueEntitySetupActions = new();
        // private readonly Queue<Action> = new();
        
        private Thread _logicalThread;
        private readonly Stopwatch _queueStopwatch = new();

        private readonly WorkerQueue _workerCleanupActions = new("engine.Engine.Cleanup");
        private readonly WorkerQueue _workerMainThreadActions = new("engine.Engine.MainThread");

        private bool _mayCallLogical = false;

        private bool _isFullscreen = false;
        
        private physics.Manager _managerPhysics;
        public readonly physics.Binding PhysicsBinding;
        public readonly SceneSequencer SceneSequencer;        
        
        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;
        
        public event EventHandler<physics.ContactInfo> OnContactInfo {
            add => PhysicsBinding.OnContactInfo += value; remove => PhysicsBinding.OnContactInfo -= value;
        } 

        
        public BepuPhysics.Simulation Simulation
        {
            get => PhysicsBinding.Simulation;
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


        public int GetNextId()
        {
            lock(_lo)
            {
                return ++_nextId;
            }
        }
        

        public engine.hierarchy.API GetAHierarchy()
        {
            return _aHierarchy;
        }


        public engine.transform.API GetATransform()
        {
            return _aTransform;
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
                Action<DefaultEcs.Entity> action;
                lock (_lo)
                {
                    if( _queueEntitySetupActions.Count==0)
                    {
                        break;
                    }
                    action = _queueEntitySetupActions.Dequeue();
                }

                DefaultEcs.Entity entity = _ecsWorld.CreateEntity();
                try {                    
                    action(entity);
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


        public void QueueEntitySetupAction(Action<DefaultEcs.Entity> action)
        {
            lock (_lo)
            {
                _queueEntitySetupActions.Enqueue(action); 
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
            PhysicsBinding.AddContactListener(entity);
        }

        public void RemoveContactListener(DefaultEcs.Entity entity)
        {
            PhysicsBinding.RemoveContactListener(entity);
        }


        /**
         * Called by the platform on a new physical frame.
         */
        public void OnPhysicalFrame(float dt)
        {
            PhysicalFrame?.Invoke(this, dt);
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
            PhysicsBinding.Simulation.Timestep(dt);

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
        

        public void GetMouseMove( out Vector2 vMouseMove )
        {
            _platform.GetMouseMove(out vMouseMove);
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
            }

        }

        /**
         * Call after all dependencies are set.
         */
        public void SetupDone()
        {
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
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
            PhysicsBinding = new physics.Binding(this);
            SceneSequencer = new(this);
        }
    }
}
 