using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
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

        private SortedDictionary<float, IScene> _dictScenes;
        private SortedDictionary<float, IPart> _dictParts;
        private SortedDictionary<string, Func<IScene>> _dictSceneFactories;
        private IScene _sceneNewMain = null;
        private IScene _mainScene = null;

        private Queue<Action<DefaultEcs.Entity>> _queueEntitySetupActions = new();
        private Queue<Action> _queueCleanupActions = new();

        private Dictionary<string, string> _dictConfigParams = new();

        private Thread _logicalThread;
        private Stopwatch _queueStopwatch = new();

        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;
        public event EventHandler<physics.ContactInfo> OnContactInfo;

        private WorkerQueue _workerCleanupActions = new("engine.Engine.Cleanup");

        private WorkerQueue _workerMainThreadActions = new("engine.Engine.MainThread");




        class EnginePhysicsEventHandler : physics.IContactEventHandler
        {
            public Simulation Simulation;
            public Engine Engine;

            public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
                in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
            {
                physics.ContactInfo contactInfo = new(
                    eventSource, pair, contactOffset, contactNormal, depth);
                Engine.OnContactInfo?.Invoke(Engine,contactInfo);
            }
        }


        public Simulation Simulation { get; private set;  }
        public BufferPool BufferPool { get; private set; }
        private physics.ContactEvents<EnginePhysicsEventHandler> _contactEvents;
        private ThreadDispatcher  _physicsThreadDispatcher;

        private physics.Manager _managerPhysics;


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

        public void AddSceneFactory(in string name, in Func<IScene> factoryFunction)
        {
            lock(_lo)
            {
                _dictSceneFactories.Add(name, factoryFunction);
            }
        }

        public void SetMainScene(in IScene scene)
        {
            lock(_lo)
            {
                _sceneNewMain = scene;
            }
        }

        private void _loadNewMainScene()
        {
            IScene scene = null;
            IScene oldScene = null;
            lock (_lo)
            {
                scene = _sceneNewMain;
                _sceneNewMain = null;
                if (null == scene)
                {
                    return;
                }
                oldScene = _mainScene;
                _mainScene = null;
            }
            if (oldScene != null)
            {
                oldScene.SceneDeactivate();
            }
            // TXWTODO: Wait for old scene to be done? No? Fadeouts??
            lock (_lo)
            {
                _mainScene = scene;
            }
            if (scene != null)
            {
                scene.SceneActivate(this);
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


        public void SetMainScene(in string name)
        {
            Func<IScene> factoryFunction = null;
            lock(_lo)
            {
                factoryFunction = _dictSceneFactories[name];
            }
            IScene scene = factoryFunction();
            SetMainScene(scene);
        }


        public void AddContactListener(DefaultEcs.Entity entity)
        {
            _contactEvents.RegisterListener(
                new CollidableReference(
                    CollidableMobility.Dynamic, 
                    entity.Get<physics.components.Body>().Reference.Handle));
        }

        public void RemoveContactListener(DefaultEcs.Entity entity)
        {
            _contactEvents.UnregisterListener(
                new CollidableReference(
                    CollidableMobility.Dynamic,
                    entity.Get<physics.components.Body>().Reference.Handle));
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
            LogicalFrame?.Invoke(this, dt);
            {
                var dictScenes = new SortedDictionary<float, IScene>(_dictScenes);
                foreach (KeyValuePair<float, IScene> kvp in dictScenes)
                {
                    kvp.Value.SceneOnLogicalFrame(dt);
                }
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
            Simulation.Timestep(dt);

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
             * Execute loading of potential new main scene.
             */
            _loadNewMainScene();

            /*
             * Async create / setup new entities.
             */
            _executeEntitySetupActions(0.001f);
            _workerMainThreadActions.RunPart(0.001f);
            _workerCleanupActions.RunPart(0.001f);
        }


        private double _timeLeft;
        private int _fpsLogical = 60;


        /**
         * Add another scene.
         */
        public void AddScene(float zOrder, in IScene scene)
        {
            _dictScenes.Add(zOrder, scene);
        }


        public void RemoveScene(in IScene scene)
        {
            foreach( KeyValuePair<float, IScene> kvp in _dictScenes )
            {
                if( kvp.Value == scene )
                {
                    _dictScenes.Remove(kvp.Key);
                    return;
                }
            }
        }

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


        public IUI CreateUI()
        {
            return _platform.CreateUI();
        }

        public void Execute()
        {
            _platform.Execute();
        }


        private void _logicalThreadFunction()
        {
            Stopwatch stopWatchSleep = new Stopwatch();
            Stopwatch stopWatchProcessing = new Stopwatch();
            const int microFrameDuration = 1000000 / 60;
            int microToWait = microFrameDuration;
            int totalPassedMicros = 0;
            int totalProcessingMicros = 0;
            
            while (_platform.IsRunning())
            {
                while(microToWait>999)
                {
                    // _platform.Sleep((int)microsToWait/1000000f);

                    int millisToWait = microToWait / 1000;
                    stopWatchSleep.Reset();
                    if (millisToWait > 0)
                    {
                        // Trace($"Logical thread sleeping {millisToWait}ms.");
                        stopWatchSleep.Start();
                        System.Threading.Thread.Sleep(millisToWait);
                        stopWatchSleep.Stop();
                    }

                    int sleepedMicros = (int)stopWatchSleep.Elapsed.TotalMicroseconds;
                    totalPassedMicros += sleepedMicros;
                    microToWait -= sleepedMicros;
                }
                stopWatchProcessing.Reset();
                stopWatchProcessing.Start();
                float df = (float)totalPassedMicros / 1000000f;
                // Trace($"Calling _onLogicalFrame({df}s).");
                _onLogicalFrame(df);
                stopWatchProcessing.Stop();
                totalProcessingMicros = (int)stopWatchProcessing.Elapsed.TotalMicroseconds;
                // Warning($"Processing of logical frame took {totalProcessingMicros}us.");
                if ( totalProcessingMicros>microFrameDuration )
                {
                    Warning($"Processing of logical frame took {totalProcessingMicros}us, longer than one logical frame({microFrameDuration}us).");
                } else
                {
                    microToWait += microFrameDuration - totalProcessingMicros;
                }
                totalPassedMicros = totalProcessingMicros;
            }
        }

        /**
         * Call after all dependencies are set.
         */
        public void SetupDone()
        {
            BufferPool = new BufferPool();
            _physicsThreadDispatcher = new(4);
            EnginePhysicsEventHandler enginePhysicsEventHandler = new();
            _contactEvents = new physics.ContactEvents<EnginePhysicsEventHandler>(
                enginePhysicsEventHandler,
                BufferPool,
                _physicsThreadDispatcher
                );
            enginePhysicsEventHandler.Engine = this;
            Simulation = Simulation.Create(
                BufferPool, 
                new physics.NarrowPhaseCallbacks<EnginePhysicsEventHandler>(_contactEvents) /* { Properties = properties } */,
                new physics.PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
                new SolveDescription(8, 1)
            );
            enginePhysicsEventHandler.Simulation = Simulation;
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
            _systemBehave = new(this);
            _systemApplyPoses = new(this);
            _systemMoveKinetics = new(this);
            _systemMovingSounds = new(this);
            _managerPhysics = new physics.Manager();
            _managerPhysics.Manage(this);

            _logicalThread = new Thread(_logicalThreadFunction);
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


        public void SetConfigParam(in string key, in string value)
        {
            lock(_lo)
            {
                _dictConfigParams[key] = value;
            }
        }


        public string GetConfigParam(in string key)
        {
            lock(_lo)
            {
                if( _dictConfigParams.ContainsKey(key))
                {
                    return _dictConfigParams[key];
                } else
                {
                    return "";
                }
            }
        }

        public Engine( engine.IPlatform platform )
        {
            _nextId = 0;
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
            _entityCommandRecorder = new(4096, 1024*1024);
            _dictScenes = new();
            _dictParts = new();
            _dictSceneFactories = new();
        }
    }
}
 