using DefaultEcs;
using DefaultEcs.Resource;
using System;
using System.Collections.Generic;

namespace engine.physics
{
    internal class Manager : IDisposable
    {
        private engine.Engine _engine;

        private void OnComponentAdded(in Entity entity, in components.Body cBody)
        {
            // We need to assume the user added the new entity.
        }

        private void OnComponentChanged(in Entity entity, in components.Body cOldBody, in components.Body cNewBody)
        {
            // We need to assume the user added the new entity.
            _engine.Simulation.Bodies.Remove(cOldBody.Handle);
        }
        private void OnComponentRemoved(in Entity entity, in components.Body cBody)
        {
            _engine.Simulation.Bodies.Remove(cBody.Handle);
        }

        public void Dispose()
        {
            _engine = null;
        }

        public IDisposable Manage(in engine.Engine engine)
        {
            _engine = engine;

            IEnumerable<IDisposable> GetSubscriptions(World w)
            {
                yield return w.SubscribeComponentAdded<components.Body>(OnComponentAdded);
                yield return w.SubscribeComponentChanged<components.Body>(OnComponentChanged);
                yield return w.SubscribeComponentRemoved<components.Body>(OnComponentRemoved);
            }
            DefaultEcs.World world = _engine.GetEcsWorld();

            return GetSubscriptions(world).Merge();
        }

        public Manager(in engine.Engine engine)
        {
        }

    }
}
