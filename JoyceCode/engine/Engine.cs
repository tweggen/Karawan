using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;

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

        private SortedDictionary<float, IScene> _dictScenes;
        private SortedDictionary<float, IPart> _dictParts;

        private Dictionary<string, string> _dictConfigParams = new();

        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;

        struct EventHandler : physics.IContactEventHandler
        {
            public Simulation Simulation;

            public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
                in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
            {
                Console.WriteLine($"OnContactAdded({eventSource}, {pair}, {contactOffset}, {contactNormal}, {depth}, {contactIndex}, {workerIndex}");
#if false
                //var other = pair.A.Packed == eventSource.Packed ? pair.B : pair.A;
                //Console.WriteLine($"Added contact: ({eventSource}, {other}): {featureId}");
                //Simply ignore any particles beyond the allocated space.
                var index = Interlocked.Increment(ref Particles.Count) - 1;
                if (index < Particles.Span.Length)
                {
                    ref var particle = ref Particles[index];

                    //Contact data is calibrated according to the order of the pair, so using A's position is important.
                    particle.Position = contactOffset + (pair.A.Mobility == CollidableMobility.Static ?
                        new StaticReference(pair.A.StaticHandle, Simulation.Statics).Pose.Position :
                        new BodyReference(pair.A.BodyHandle, Simulation.Bodies).Pose.Position);
                    particle.Age = 0;
                    particle.Normal = contactNormal;
                }
#endif
            }
        }


        public Simulation Simulation { get; protected set; }
        public BufferPool BufferPool { get; private set; }
        private physics.ContactEvents<EventHandler> _events;

        private physics.Manager _managerPhysics;

        private void _selfTest()
        {
            /*
             * Create a simple hierarchy test case
             */
            {
                var eParent = _ecsWorld.CreateEntity();
                var eKid1 = _ecsWorld.CreateEntity();
                var eKid2 = _ecsWorld.CreateEntity();

                _aHierarchy.SetParent(eKid1, eParent);
                _aHierarchy.SetParent(eKid2, eParent);
                _aHierarchy.Update();
                _aHierarchy.SetParent(eKid1, null);
                _aHierarchy.SetParent(eKid2, eKid1);
                _aHierarchy.SetParent(eKid2, eParent);
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

        public DefaultEcs.Entity CreateEntity()
        {
            return _ecsWorld.CreateEntity();
        }


        public void _onLogicalFrame(float dt)
        {
            _systemBehave.Update(dt);
            LogicalFrame?.Invoke(this, dt);

            Simulation.Timestep(dt);

            /*
             * We need a local copy in case anybody adds scenes.
             */
            var dictScenes = new SortedDictionary<float, IScene>(_dictScenes);
            foreach( KeyValuePair<float, IScene> kvp in dictScenes )
            {
                kvp.Value.SceneOnLogicalFrame(dt);
            }
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

                _aHierarchy.Update();
                _systemApplyPoses.Update(dt);
                _aTransform.Update();
            } while (_timeLeft > 0);

            _onPhysicalFrame(dt);
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
            Simulation = Simulation.Create(
                BufferPool, 
                new physics.NarrowPhaseCallbacks<EventHandler>() /* { Properties = properties } */,
                new physics.PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
                new PositionLastTimestepper()
            );
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
            _systemBehave = new(this);
            _systemApplyPoses = new(this);
            _managerPhysics = new physics.Manager();
            _managerPhysics.Manage(this);
        }


        public void PlatformSetupDone()
        {
            _selfTest();
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
        }
    }
}
