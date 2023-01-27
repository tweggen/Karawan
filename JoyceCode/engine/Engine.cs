using System.Collections.Generic;

namespace engine
{
    public class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private SortedDictionary<float, IScene> _dictScenes;

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

        public DefaultEcs.Entity CreateEntity()
        {
            return _ecsWorld.CreateEntity();
        }


        public void _onLogicalFrame(float dt)
        {
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
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
        }


        public void PlatformSetupDone()
        {
            _selfTest();
        }


        public Engine( engine.IPlatform platform )
        {
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
            _dictScenes = new();
        }
    }
}
