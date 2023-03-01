using DefaultEcs;
using System;
using System.Collections.Generic;

namespace engine.physics
{
    internal class Manager
    {
        private engine.Engine _engine;


        private void _removeStatics(in components.Statics statics)
        {
            if (statics.Handles != null)
            {
                foreach (var handle in statics.Handles)
                {
                    _engine.Simulation.Statics.Remove(handle);
                }
            }
            if (statics.ReleaseActions != null)
            {
                foreach (var releaseAction in statics.ReleaseActions)
                {
                    releaseAction();
                }
            }
        }

        private void OnStaticsChanged(in Entity entity, in components.Statics cOldStatics, in components.Statics cNewStatics)
        {
            // We need to assume the user added the new entity.
            _removeStatics(cOldStatics);
        }
        private void OnStaticsRemoved(in Entity entity, in components.Statics cStatics)
        {
            _removeStatics(cStatics);
        }


        private void OnBodyChanged(in Entity entity, in components.Body cOldBody, in components.Body cNewBody)
        {
            // We need to assume the user added the new entity.
            _engine.Simulation.Bodies.Remove(cOldBody.Reference.Handle);
        }
        private void OnBodyRemoved(in Entity entity, in components.Body cBody)
        {
            _engine.Simulation.Bodies.Remove(cBody.Reference.Handle);
        }

        public void Dispose()
        {
            _engine = null;
        }

        public void Manage(in engine.Engine engine)
        {
            _engine = engine;

            IEnumerable<IDisposable> GetSubscriptions(World w)
            {
                // yield return w.SubscribeComponentAdded<components.Body>(OnComponentAdded);
                yield return w.SubscribeComponentChanged<components.Body>(OnBodyChanged);
                yield return w.SubscribeComponentRemoved<components.Body>(OnBodyRemoved);
                yield return w.SubscribeComponentChanged<components.Statics>(OnStaticsChanged);
                yield return w.SubscribeComponentRemoved<components.Statics>(OnStaticsRemoved);
            }
            DefaultEcs.World world = _engine.GetEcsWorld();

            GetSubscriptions(world);
        }

        public Manager()
        {
        }

    }
}
