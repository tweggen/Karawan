using System.Collections.Generic;

namespace engine
{
    public class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private List<IScene> _listScenes;

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


        public void _onLogicalFrame(float dt)
        {
            /*
             * We need a local copy in case anybody adds scenes.
             */
            var listScenes = new List<IScene>(_listScenes);
            foreach( var scene in listScenes )
            {
                scene.SceneOnLogicalFrame(dt);
            }
        }

        public void OnPhysicalFrame(float dt)
        {
            // TXWTODO: Iterate through logical frames.

            /*
             * First, let the scenes update themselves.
             */
            _onLogicalFrame(dt);

            _aHierarchy.Update();
            _aTransform.Update();
        }

        /**
         * Add another scene.
         */
        public void AddScene(IScene scene)
        {
            _listScenes.Add(scene);
        }


        public void RemoveScene(IScene scene)
        {
            _listScenes.Remove(scene);
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
            _listScenes = new();
        }
    }
}
