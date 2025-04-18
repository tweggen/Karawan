using System;
using System.Numerics;
using BepuPhysics.Collidables;
using engine;
using engine.draw;
using engine.physics;

namespace nogame.modules.playerhover;


/**
 * If active, display something about the object right in the middle
 * of the display.
 */
public class IdentifyCameraFocusModule : engine.AModule
{
    private DefaultEcs.Entity _eCamera;
    
    private DateTime _timestampFacingObject = DateTime.Now;
    private string _strFacingObject = "";
    private CollisionProperties _cpFacingObject = null;

    /**
     * Display the object we are facing.
     */
    private DefaultEcs.Entity _eTargetDisplay;


    
    
    private void _onCameraEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_eCamera != entity)
            {
                _eCamera = entity;
                isChanged = true;
            }
        }
    }

    
    private void _onCenterRayHit(
        CollidableReference collidableReference,
        CollisionProperties collisionProperties,
        float t,
        Vector3 vNormal)
    {
        if (null != collisionProperties)
        {
            lock (_lo)
            {
                _timestampFacingObject = DateTime.Now;
                if (_cpFacingObject != collisionProperties)
                {
                    _cpFacingObject = collisionProperties;
                    _strFacingObject =
                        $"{collisionProperties.Entity} {collisionProperties.Name} ({collisionProperties.DebugInfo})";
                }
            }
        }
    }
    
    
    private void _testResetFacingObject()
    {
        lock (_lo)
        {
            if (_cpFacingObject != null)
            {
                if ((DateTime.Now - _timestampFacingObject).TotalMilliseconds > 1000)
                {
                    _cpFacingObject = null;
                    _strFacingObject = "";
                }
            }
        }
    }
    

    private void _onLogicalFrame(object? sender, float dt)
    {
        /*
         * Look up the object we are facing.
         */
        if (false)
        {
            if (_eCamera.IsAlive)
            {
                if (_eCamera.Has<engine.joyce.components.Transform3ToWorld>() &&
                    _eCamera.Has<engine.joyce.components.Camera3>())
                {
                    var cCamTransform = _eCamera.Get<engine.joyce.components.Transform3ToWorld>();
                    var cCamera = _eCamera.Get<engine.joyce.components.Camera3>();
                    var mCameraToWorld = cCamTransform.Matrix;
                    Vector3 vZ = new Vector3(mCameraToWorld.M31, mCameraToWorld.M32, mCameraToWorld.M33);
                    var vCamPosition = mCameraToWorld.Translation;

                    I.Get<engine.physics.API>().RayCast(vCamPosition, -vZ, 200f, _onCenterRayHit);
                }
            }

            _testResetFacingObject();
        }

        if (false)
        {
            string strFacingObject;
            lock (_lo)
            {
                strFacingObject = _strFacingObject;
            }

            float width = 320f;
            _eTargetDisplay.Set(new engine.draw.components.OSDText(
                new Vector2((786f - width) / 2f, 360f),
                new Vector2(width, 18f),
                $"{strFacingObject}",
                10,
                0xff22aaee,
                0x00000000,
                HAlign.Center));
        }

    }


    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;

        DefaultEcs.Entity eTargetDisplay = _eTargetDisplay;
        _eTargetDisplay = default;
        _engine.QueueCleanupAction(() =>
        {
            eTargetDisplay.Disable();
            _engine.AddDoomedEntity(eTargetDisplay);
        });
        
        _engine.Camera.RemoveOnChange(_onCameraEntityChanged);

        _engine.RemoveModule(this);
        
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        
        _eTargetDisplay = _engine.CreateEntity("OsdTargetDisplay");

        if (_engine.Camera.TryGet(out var eCam))
        {
            _onCameraEntityChanged(eCam);
        }
        _engine.Camera.AddOnChange(_onCameraEntityChanged);
        
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}