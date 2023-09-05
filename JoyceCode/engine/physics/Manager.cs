using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static engine.Logger;

namespace engine.physics
{
    
    /**
     * Observes entities with physics components, adding/removing them from or
     * to the physics engine system if required.
     */
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


        private void _removeKinetic(in components.Kinetic cKinetic)
        {
            if (0 == (cKinetic.Flags & components.Kinetic.DONT_FREE_PHYSICS))
            {
                _engine.Simulation.Bodies.Remove(cKinetic.Reference.Handle);
            }
            else
            {
                Trace("Hit me.");
            }

            if (cKinetic.ReleaseActions != null)
            {
                foreach (var releaseAction in cKinetic.ReleaseActions)
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


        private void OnKineticChanged(in Entity entity, in components.Kinetic cOldKinetic, in components.Kinetic cNewKinetic)
        {
            if (cOldKinetic.Reference.Handle.Value != cNewKinetic.Reference.Handle.Value)
            {
                // We need to assume the user added the new entity.
                _removeKinetic(cOldKinetic);
            }
        }

        
        private void OnKineticRemoved(in Entity entity, in components.Kinetic cKinetic)
        {
            _removeKinetic(cKinetic);
        }


        private void OnBodyChanged(in Entity entity, in components.Body cOldBody, in components.Body cNewBody)
        {
            if (cOldBody.Reference.Handle.Value != cNewBody.Reference.Handle.Value)
            {
                // We need to assume the user added the new entity.
                if (0 == (cOldBody.Flags & components.Body.DONT_FREE_PHYSICS))
                {
                    _engine.Simulation.Bodies.Remove(cOldBody.Reference.Handle);
                }
            }
        }
        
        private void OnBodyRemoved(in Entity entity, in components.Body cBody)
        {
            if (0 == (cBody.Flags & components.Body.DONT_FREE_PHYSICS))
            {
                _engine.Simulation.Bodies.Remove(cBody.Reference.Handle);
            }
        }

        public void Dispose()
        {
            _engine = null;
        }

        public void Manage(in engine.Engine engine)
        {
            _engine = engine;

            /* IEnumerable<IDisposable>*/ void GetSubscriptions(World w)
            {
                // yield return w.SubscribeComponentAdded<components.Body>(OnComponentAdded);
                /* yield return */ w.SubscribeEntityComponentChanged<components.Body>(OnBodyChanged);
                /* yield return */ w.SubscribeEntityComponentRemoved<components.Body>(OnBodyRemoved);
                /* yield return */ w.SubscribeEntityComponentChanged<components.Kinetic>(OnKineticChanged);
                /* yield return */ w.SubscribeEntityComponentRemoved<components.Kinetic>(OnKineticRemoved);
                /* yield return */ w.SubscribeEntityComponentChanged<components.Statics>(OnStaticsChanged);
                /* yield return */ w.SubscribeEntityComponentRemoved<components.Statics>(OnStaticsRemoved);
            }
            DefaultEcs.World world = _engine.GetEcsWorld();

            GetSubscriptions(world);
        }

        public Manager()
        {
        }

    }
}
