using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuUtilities;
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

        private SortedDictionary<float, IScene> _dictScenes;

        private Dictionary<string, string> _dictConfigParams = new();

        public event EventHandler<float> LogicalFrame;
        public event EventHandler<float> PhysicalFrame;
        public event EventHandler<uint> KeyEvent;

        public Simulation Simulation { get; protected set; }
        public BufferPool BufferPool { get; private set; }

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
                _aTransform.Update();
            } while (_timeLeft > 0);

            _onPhysicalFrame(dt);
        }

        /**
         * Add another scene.
         */
        public void AddScene(float zOrder, IScene scene)
        {
            _dictScenes.Add(zOrder, scene);
        }


        public void RemoveScene(IScene scene)
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

        public void Render3D()
        {
            _platform.Render3D();
        }

        public void GetControllerState( out ControllerState controllerState)
        {
            _platform.GetControllerState( out controllerState );
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
                new physics.NarrowPhaseCallbacks() /* { Properties = properties } */,
                new physics.PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
                new PositionLastTimestepper()
            );
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
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
        }
    }
}
