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
        private List<IList<DefaultEcs.Entity>> _listDoomedEntityLists;

        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private engine.behave.systems.BehaviorSystem _systemBehave;
        private engine.physics.systems.ApplyPosesSystem _systemApplyPoses;
        private engine.physics.systems.MoveKineticsSystem _systemMoveKinetics;

        private SortedDictionary<float, IScene> _dictScenes;
        private SortedDictionary<float, IPart> _dictParts;
        private SortedDictionary<string, Func<IScene>> _dictSceneFactories;

        private Dictionary<string, string> _dictConfigParams = new();

        private Thread _logicalThread;

        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;
        public event EventHandler<physics.ContactInfo> OnContactInfo;



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


        public Simulation Simulation { get; protected set; }
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

        private IScene _mainScene = null;
        public void SetMainScene(in IScene scene)
        {
            IScene oldScene = null;
            lock(_lo)
            {
                oldScene = _mainScene;
                _mainScene = null;
            }
            if (oldScene != null)
            {
                oldScene.SceneDeactivate();
            }
            // TXWTODO: Wait for old scene to be done? No? Fadeouts??
            lock(_lo)
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
            lock(_lo)
            {
                listList = _listDoomedEntityLists;
                _listDoomedEntityLists = null;
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

            _commitWorldRecord();

            _platform.CollectRenderData();

            _executeDoomedEntities();
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


        public void GetControllerState( out ControllerState controllerState)
        {
            _platform.GetControllerState( out controllerState );
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
            Stopwatch stopWatch = new Stopwatch();
            int skippedLogical = 0;
            const int microWaitDuration = 1000000 / 60;
            int microToWait = microWaitDuration;
            int totalPassedMicros = 0;
            stopWatch.Start();
            while (true)
            {
                while(microToWait>999)
                {
                    stopWatch.Stop();
                    int passedMicros = (int)stopWatch.Elapsed.TotalMicroseconds;
                    stopWatch.Start();
                    totalPassedMicros += passedMicros;

                    if (passedMicros > microToWait)
                    {
                        Warning("Logical thread took {passedMicros}us, that is longer than {microToWait}us");
                        ++skippedLogical;
                        microToWait = 0;
                    } else
                    {
                        microToWait -= passedMicros;

                        /*
                         * Look, if we still need to wait.
                         */

                        int millisToWait = microToWait / 1000;
                        if (millisToWait > 0)
                        {
                            System.Threading.Thread.Sleep(millisToWait);
                        }
                    }
                }
                _onLogicalFrame((float)totalPassedMicros / 1000f);
                totalPassedMicros = 0;
                microToWait += microWaitDuration;
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
            _managerPhysics = new physics.Manager();
            _managerPhysics.Manage(this);

            _logicalThread = new Thread(_logicalThreadFunction);
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
 