using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using IronPython.Compiler;

namespace engine
{
    public class Engine
    {
        private object _lo = new();

        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private engine.behave.systems.BehaviorSystem _systemBehave;
        private engine.physics.systems.ApplyPosesSystem _systemApplyPoses;
        private engine.physics.systems.MoveKineticsSystem _systemMoveKinetics;

        private SortedDictionary<float, IScene> _dictScenes;
        private SortedDictionary<float, IPart> _dictParts;

        private Dictionary<string, string> _dictConfigParams = new();

        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;
        public event EventHandler<physics.ContactInfo> OnContactInfo;

        private SortedDictionary<string, Func<IScene>> _dictSceneFactories;

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

        public void AddInstance3(
            DefaultEcs.Entity eSelf,
            bool isVisible,
            uint cameraMask,
            in Vector3 vPosition,
            in Quaternion qRotation)
        {
            _aTransform.SetTransforms(
                eSelf,
                isVisible,
                cameraMask,
                qRotation,
                vPosition);
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

        public DefaultEcs.Entity CreateEntity()
        {
            return _ecsWorld.CreateEntity();
        }

        private bool _firstTime = true;

        public void _onLogicalFrame(float dt)
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
                 * Move kinetics requires 
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

        }


        public void _onPhysicalFrame(float dt)
        {
            PhysicalFrame?.Invoke(this, dt);

            /*
             * We need a local copy in case anybody adds scenes.
             */
            var dictScenes = new SortedDictionary<float, IScene>(_dictScenes);
            foreach (KeyValuePair<float, IScene> kvp in dictScenes)
            {
                kvp.Value.SceneOnPhysicalFrame(dt);
            }
        }

        private double _timeLeft;
        private int _fpsLogical = 60;

        public void OnPhysicalFrame(float dt)
        {
            _timeLeft += dt;
            do
            {
                _timeLeft -= 1 / (double)_fpsLogical;

                /*
                 * First, let the scenes update themselves.
                 */
                _onLogicalFrame((float)(1/(double)_fpsLogical));
            } while (_timeLeft > 0);

            _onPhysicalFrame(dt);
            Render3D();
        }

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

        public void Render3D()
        {
            _platform.Render3D();
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
        }


        public void PlatformSetupDone()
        {
            {
                /*
                 * Zero out initial accumulated mouse move.
                 */
                GetMouseMove(out _);
            }
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
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
            _dictScenes = new();
            _dictParts = new();
            _dictSceneFactories = new();
        }
    }
}
