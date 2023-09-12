#if false

using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools;

class CameraEntry
{
    public DefaultEcs.Entity Entity;
    public engine.joyce.components.Camera3 Camera3;
}

/**
 * Keep track of all active camera entities, enabling fast access.
 */
public class CameraWatcher : IDisposable
{
    private object _lo = new();
    
    private engine.Engine _engine;

    private IDisposable? _subscriptions;

    private SortedDictionary<uint, CameraEntry> _mapCameras;

    public void _onCameraRemoved(in DefaultEcs.Entity entity, 
        in engine.joyce.components.Camera3 cOldBehavior)
    {
    }


    public void _onCameraChanged(in DefaultEcs.Entity entity,
        in engine.joyce.components.Camera3 cOldCamera,
        in engine.joyce.components.Camera3 cNewCamera)
    {
        lock (_lo)
        {
            if (_mapCameras.TryGetValue(cNewCamera.CameraMask, out var ce))
            {
            }
        }
    }


    public void _onCameraAdded(in DefaultEcs.Entity entity, 
        in engine.joyce.components.Camera3 cNewCamera)
    {
        lock (_lo)
        {
            if (_mapCameras.TryGetValue(cNewCamera.CameraMask, out var ce))
            {
                /*
                 * Already existing camera entry? ignore it.
                 */
                Error($"Already have a camera for mask {cNewCamera.CameraMask}");
                return;
            }

            ce = new()
            {
                Entity = entity,
                Camera3 = cNewCamera
            };
            _mapCameras.Add(cNewCamera.CameraMask, ce);
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
    
    
    public void Manage(in engine.Engine engine0)
    {
        _engine = engine0;

        _mapCameras = new();
        
        IEnumerable<IDisposable> GetSubscriptions(DefaultEcs.World w)
        {
            yield return w.SubscribeEntityComponentAdded<engine.joyce.components.Camera3>(_onCameraAdded);
            yield return w.SubscribeEntityComponentChanged<engine.joyce.components.Camera3>(_onCameraChanged);
            yield return w.SubscribeEntityComponentRemoved<engine.joyce.components.Camera3>(_onCameraRemoved);
        }
        DefaultEcs.World world = _engine.GetEcsWorld();
        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        var entities = world.GetEntities().With<engine.joyce.components.Camera3>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onCameraAdded(entity, entity.Get<engine.joyce.components.Camera3>());
        }

        _subscriptions = GetSubscriptions(world).Merge();
    }

    private void ErrorThrow(string worldMustNotBeNull, Func<object, ArgumentException> func)
    {
        throw new NotImplementedException();
    }


    public void Dispose()
    {
        Stop();
        _mapCameras = null;
        _engine = null;
    }

}
#endif