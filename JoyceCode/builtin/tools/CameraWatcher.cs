using engine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using engine.joyce;
using engine.joyce.components;
using static engine.Logger;

namespace builtin.tools;

public class CameraEntry
{
    public uint ComputedAt;
    public DefaultEcs.Entity Entity;
    public bool IsVisible; 
    public engine.joyce.components.Camera3 CCamera;
    public engine.joyce.components.Transform3ToWorld CCamTransform;
    public Vector3 V3CamPosition;
    public Matrix4x4 MCameraToWorld;
    public Matrix4x4 MView;
    public Matrix4x4 MProjection;
}

/**
 * Keep track of all active camera entities, enabling fast access.
 */
public class CameraWatcher : AModule
{
    /**
     * We are keep are keeping the subscriptions for the camera objects in our class. 
     */
    private IDisposable? _subscriptions;

    /*
     * The frame number we fetched the camera info.
     */
    private uint _computedAt = 0;
    private SortedDictionary<uint, CameraEntry> _mapCameras;
    
    
    public void _onCameraRemoved(in DefaultEcs.Entity entity, in Camera3 cOldCamera)
    {
        _mapCameras.Remove(cOldCamera.CameraMask);
    }


    public void _onCameraChanged(in DefaultEcs.Entity eCamera, in Camera3 cOldCamera, in Camera3 cNewCamera)
    {
        lock (_lo)
        {
            if (_mapCameras.TryGetValue(cOldCamera.CameraMask, out var ce))
            {
                bool isVisible = false;

                if (eCamera.IsAlive && eCamera.IsEnabled()
                                    && eCamera.Has<Transform3ToWorld>())
                {
                    if (cOldCamera.CameraMask != cNewCamera.CameraMask)
                    {
                        _mapCameras.Remove(cOldCamera.CameraMask);
                        _mapCameras.Add(cNewCamera.CameraMask, ce);
                    }

                    ce.CCamera = cNewCamera;
                    ce.CCamTransform = eCamera.Get<Transform3ToWorld>();
                    if (ce.CCamTransform.IsVisible)
                    {
                        isVisible = true;
                    }
                    
                    if (!isVisible)
                    {
                        ce.IsVisible = false;
                    }
                    else
                    {
                        ce.IsVisible = true;
                    }

                    ce.ComputedAt = 0;
                }
            }
        }
    }


    private void _update()
    {
        if (null == _engine) return;
        var frameNumber = _engine.FrameNumber;

        lock (_lo)
        {
            if (_computedAt == frameNumber)
            {
                return;
            }


            foreach (var kvp in _mapCameras)
            {
                ref var eCamera = ref kvp.Value.Entity;
                bool isVisible = false;
                CameraEntry ce = kvp.Value;
                
                if (eCamera.IsAlive && eCamera.IsEnabled()
                                    && eCamera.Has<Transform3ToWorld>()
                                    && eCamera.Has<Camera3>())
                {
                    kvp.Value.CCamTransform = eCamera.Get<Transform3ToWorld>();
                    if (kvp.Value.CCamTransform.IsVisible)
                    {
                        isVisible = true;
                    }
                }

                if (!isVisible)
                {
                    kvp.Value.IsVisible = false;
                }
                else
                {
                    kvp.Value.IsVisible = true;
                    ce.CCamera = eCamera.Get<Camera3>();
                    Renderbuffer renderbuffer = ce.CCamera.Renderbuffer;
                    Vector2 v2ViewSize;
                    if (null != renderbuffer)
                    {
                        v2ViewSize = new(renderbuffer.Width, renderbuffer.Height);
                    }
                    else
                    {
                        v2ViewSize = engine.GlobalSettings.ParseSize(engine.GlobalSettings.Get("view.size"));
                    }

                    ce.MCameraToWorld = ce.CCamTransform.Matrix;
                    ce.V3CamPosition = ce.MCameraToWorld.Translation;

                    ce.CCamera.GetProjectionMatrix(out ce.MProjection, v2ViewSize);
                }

                ce.CCamera.GetViewMatrix(out ce.MView, ce.MCameraToWorld);
                kvp.Value.ComputedAt = frameNumber;
                
            }

            _computedAt = frameNumber;
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
                ComputedAt = 0,
                Entity = entity,
                CCamera = cNewCamera
            };
            _mapCameras.Add(cNewCamera.CameraMask, ce);

            _computedAt = 0;
        }
    }


    public CameraEntry? GetCameraEntry(uint cameraMask)
    {
        if (null == _engine)
        {
            return null;
        }
        
        _update();
        
        lock (_lo)
        {
            if (null == _mapCameras)
            {
                return null;
            }

            if (!_mapCameras.TryGetValue(cameraMask, out var ce))
            {
                return null;
            }

            return ce;
        }
    }
    

    protected override void OnModuleDeactivate()
    {
        if (_subscriptions != null)
        {
            _subscriptions.Dispose();
            _subscriptions = null;
        }
    }


    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        lock (_lo)
        {
            _mapCameras = new();
        }

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
}
