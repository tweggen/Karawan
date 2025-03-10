﻿ using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using static engine.Logger;

namespace engine.physics;

/**
 * Observes entities with physics components, adding/removing them from or
 * to the physics engine system if required.
 */
internal class Manager
{
    private engine.Engine _engine;

    private IDisposable? _subscriptions;
    

    private void _addStaticsNoLock(in components.Statics statics)
    {
        if (statics.PhysicsObject != null)
        {
            statics.PhysicsObject.Activate();
        }
    }
    
    
    private void _removeStaticsNoLock(in components.Statics statics)
    {
        int poHandle = -1;
        if (statics.PhysicsObject != null)
        {
            poHandle = statics.PhysicsObject.IntHandle;
            statics.PhysicsObject.MarkDeleted();
            statics.PhysicsObject.Dispose();
        }
        
        if (statics.Handles != null)
        {
            foreach (var handle in statics.Handles)
            {
                if (handle.Value != poHandle)
                {
                    _engine.Simulation.Statics.Remove(handle);
                }
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


    private void _onStaticsChanged(in Entity entity, in components.Statics cOldStatics,
        in components.Statics cNewStatics)
    {
        lock (_engine.Simulation)
        {
            // We need to assume the user added the new entity.
            _removeStaticsNoLock(cOldStatics);
            _addStaticsNoLock(cNewStatics);
        }
    }


    private void _onStaticsRemoved(in Entity entity, in components.Statics cStatics)
    {
        lock (_engine.Simulation)
        {
            _removeStaticsNoLock(cStatics);
        }
    }


    private void _onStaticsAdded(in Entity entity, in components.Statics cStatics)
    {
        lock (_engine.Simulation)
        {
            _addStaticsNoLock(cStatics);
        }
    }
    

    private void _removeBodyNoLock(in Entity entity, in components.Body cBody)
    {
        if (cBody.PhysicsObject != null)
        {
            cBody.PhysicsObject.MarkDeleted();
            cBody.PhysicsObject.Dispose();
        }
    }


    private void _addBodyNoLock(in Entity entity, in components.Body cBody)
    {
        if (cBody.PhysicsObject != null)
        {
            cBody.PhysicsObject.Activate();
        }
    }


    private void _onBodyChanged(in Entity entity, in components.Body cOldBody, in components.Body cNewBody)
    {
        lock (_engine.Simulation)
        {
            if (cOldBody.PhysicsObject != cNewBody.PhysicsObject)
            {
                _removeBodyNoLock(entity, cOldBody);
                _addBodyNoLock(entity, cNewBody);
            }
        }
    }


    private void _onBodyRemoved(in Entity entity, in components.Body cBody)
    {
        lock (_engine.Simulation)
        {
            _removeBodyNoLock(entity, cBody);
        }
    }


    private void _onBodyAdded(in Entity entity, in components.Body cBody)
    {
        lock (_engine.Simulation)
        {
            _addBodyNoLock(entity, cBody);
        }
    }

    
    public void Stop()
    {
        if (_subscriptions != null)
        {
            _subscriptions.Dispose();
            _subscriptions = null;
        }
    }
    
    
    public void Manage(in engine.Engine engine)
    {
        _engine = engine;

        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            // yield return w.SubscribeComponentAdded<components.Body>(OnComponentAdded);
            yield return w.SubscribeEntityComponentAdded<components.Body>(_onBodyAdded);
            yield return w.SubscribeEntityComponentChanged<components.Body>(_onBodyChanged);
            yield return w.SubscribeEntityComponentRemoved<components.Body>(_onBodyRemoved);
            yield return w.SubscribeEntityComponentAdded<components.Statics>(_onStaticsAdded);
            yield return w.SubscribeEntityComponentChanged<components.Statics>(_onStaticsChanged);
            yield return w.SubscribeEntityComponentRemoved<components.Statics>(_onStaticsRemoved);
        }

        DefaultEcs.World world = _engine.GetEcsWorld();
        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        var dynamicEntities = world.GetEntities().With<components.Body>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in dynamicEntities)
        {
            _onBodyAdded(entity, entity.Get<components.Body>());
        }
        var staticEntities = world.GetEntities().With<components.Statics>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in staticEntities)
        {
            _onStaticsAdded(entity, entity.Get<components.Statics>());
        }


        _subscriptions = GetSubscriptions(world).Merge();
    }
    
    
    public void Dispose()
    {
        Stop();
        _engine = null;
    }
}

