using System;
using System.Numerics;
using engine.joyce.components;
using engine.news;
using static engine.Logger;

namespace engine.behave.systems;


/**
 * Scan all clickable objects for the object the user might have clicked on.
 */
public class ClickableHandler
{
    private engine.Engine _engine;
    private DefaultEcs.Entity _eCamera;
    private Camera3 _cCamera3;
    private joyce.components.Transform3ToWorld _cCamTransform;
    private Vector2 _vViewSize;
    private Matrix4x4 _mProjection;
    private Matrix4x4 _mView;

    private bool _findAt(in Vector2 pos, out DefaultEcs.Entity resultingEntity, out Vector2 v2RelPos)
    {
        /*
         * We have two different version of Clickables: Those from 3d space
         * including camera mask etc. and those purely based on OSD text.
         * We handle the ones from 3d space.
         *
         * However, the framebuffer also is a surface in 3d space. It has a dedicated handler
         * attached that will have an event emitted.
         */
        /*
         * Iterate through all Clickables that also have
         * - a transform2world to get the camera mask from
         * - an Instance3 to get the aabb from.
         */
        var clickableEntities = _engine.GetEcsWorld().GetEntities()
            .With<components.Clickable>()
            .With<joyce.components.Transform3ToWorld>()
            .With<joyce.components.Instance3>()
            .AsEnumerable();

        float minZ = Single.MaxValue;
        resultingEntity = default;
        v2RelPos = default;
        
        foreach (var entity in clickableEntities)
        {
            var cTransform = entity.Get<joyce.components.Transform3ToWorld>();
            if (cTransform.Matrix.Translation.Z >= minZ) continue;
            minZ = cTransform.Matrix.Translation.Z;
            
            /*
             * Is it visible by the camera we are looking for?
             */
            if (0 == (cTransform.CameraMask & _cCamera3.CameraMask)) continue;
            
            /*
             * Now we can transform the AABB into viewable space and look if pos is inside.
             */

            joyce.InstanceDesc id = entity.Get<Instance3>().InstanceDesc;
            Vector4 vAA4 = Vector4.Transform(id.AABBTransformed.AA, cTransform.Matrix * _mView * _mProjection);
            Vector4 vBB4 = Vector4.Transform(id.AABBTransformed.BB, cTransform.Matrix * _mView * _mProjection);
            Vector2 vAA2 = new(
                (vAA4.X / vAA4.W + 1f) * _vViewSize.X / 2f, 
                (-vAA4.Y / vAA4.W + 1f) * _vViewSize.Y / 2f);
            Vector2 vBB2 = new(
                (vBB4.X / vBB4.W + 1f) * _vViewSize.X / 2f, 
                (-vBB4.Y / vBB4.W + 1f) * _vViewSize.Y / 2f);
            Vector2 ul = Vector2.Min(vAA2, vBB2);
            Vector2 lr = Vector2.Max(vAA2, vBB2);
            
            // Trace($"pos is {pos} Transformed position is ul={ul}, lr={lr}");

            if (!
                (pos.X >= ul.X && pos.X < lr.X 
                && pos.Y >= ul.Y && pos.Y < lr.Y))
            {
                continue;
            }

            v2RelPos = new((pos.X - ul.X) / (lr.X - ul.X), (pos.Y - ul.Y) / (lr.Y - ul.Y));
            
            /*
             * This is a hit.
             */
            // Trace($"Clickable {entity} was clicked.");
            resultingEntity = entity;
        }

        if (resultingEntity != default)
        {
            return true;
        }
        
        return false;
    }


    private void _updateFromCamera()
    {
        _cCamTransform = _eCamera.Get<joyce.components.Transform3ToWorld>();
        _cCamera3.GetProjectionMatrix(out _mProjection, _vViewSize);
        _cCamera3.GetViewMatrix(out _mView, _cCamTransform.Matrix);
    }
    
    
    public DefaultEcs.Entity OnClick(engine.news.Event ev)
    {
        _vViewSize = ev.Size;
        _updateFromCamera();
        
        switch (ev.Type)
        {
            case engine.news.Event.INPUT_MOUSE_PRESSED:
            case engine.news.Event.INPUT_TOUCH_PRESSED:
                /*
                 * Continue to process it.
                 */
                break;
            default:
                return default;
                break;
        }

        Vector2 pos = ev.Position;
        if (_findAt(pos, out var eFound, out var v2RelPos))
        {
            var cClickable = eFound.Get<engine.behave.components.Clickable>();
            var factory = cClickable.ClickEventFactory;
            if (factory != null)
            {
                var cev = factory(eFound, ev, v2RelPos);
                if (cev != null)
                {
                    I.Get<EventQueue>().Push(cev);
                }
            }
        }

        return eFound;
    }

    public ClickableHandler(Engine engine0, DefaultEcs.Entity eCamera)
    {
        _engine = engine0;
        _eCamera = eCamera;
        if (!_eCamera.IsAlive || !_eCamera.Has<Camera3>())
        {
            ErrorThrow($"Camera entity {eCamera} is not alive or camera has no camera3 component.",
                m => new ArgumentException(m));
            return;
        }

        _cCamera3 = _eCamera.Get<Camera3>();

    }
}