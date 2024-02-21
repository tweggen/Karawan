using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using engine.joyce.components;
using engine.news;
using static engine.Logger;

namespace engine.news;


/**
 * Scan all clickable objects for the object the user might have clicked on.
 */
public class ClickableHandler
{
    private engine.Engine _engine;
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
            .With<behave.components.Clickable>()
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
            
            /*
             * Transform the aabb to screenspace opengl coordinates, i.e. -1 ... 1
             */
            Vector4 vAA4 = Vector4.Transform(id.AABBTransformed.AA, cTransform.Matrix * _mView * _mProjection);
            Vector4 vBB4 = Vector4.Transform(id.AABBTransformed.BB, cTransform.Matrix * _mView * _mProjection);
            // TXWTODO: In addition, consider UL and LR from the actual Camera3 data structure.
            
            /*
             * Scale it up to screen space coordinates.
             */
            Vector2 vAA2;
            Vector2 vBB2;

            {
                _cCamera3.ScreenExtent(_vViewSize, out var v2ScrUl, out var v2ScrLr);
                Vector2 size = v2ScrLr-v2ScrUl;
                vAA2 = v2ScrUl + new Vector2(
                    (vAA4.X / vAA4.W + 1f) * size.X / 2f,
                    (-vAA4.Y / vAA4.W + 1f) * size.Y / 2f);
                vBB2 = v2ScrUl + new Vector2(
                    (vBB4.X / vBB4.W + 1f) * size.X / 2f,
                    (-vBB4.Y / vBB4.W + 1f) * size.Y / 2f);
            }

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


    private void _updateFromCamera(DefaultEcs.Entity eCamera)
    {
        _cCamera3 = eCamera.Get<Camera3>();
        _cCamTransform = eCamera.Get<joyce.components.Transform3ToWorld>();
        _cCamera3.GetProjectionMatrix(out _mProjection, _vViewSize);
        _cCamera3.GetViewMatrix(out _mView, _cCamTransform.Matrix);
    }
    
    
    public void OnClick(engine.news.Event ev)
    {
        _vViewSize = ev.Size;
        
        switch (ev.Type)
        {
            case engine.news.Event.INPUT_MOUSE_PRESSED:
            case engine.news.Event.INPUT_TOUCH_PRESSED:
                /*
                 * Continue to process it.
                 */
                break;
            default:
                return;
        }

        /*
         * Now iterate through all cameras.
         * We need a copy because event handlers shall be able to create/remove entities.
         */
        var cameras = new List<DefaultEcs.Entity>(_engine.GetEcsWorld().GetEntities()
            .With<Camera3>()
            .With<joyce.components.Transform3ToWorld>()
            .AsEnumerable());
        foreach (var eCamera in cameras)
        {
            _updateFromCamera(eCamera);

            Vector2 pos = ev.Position;

            /*
             * First check if the camera view is under that position.
             */
            if (!_cCamera3.ContainsScreenPosition(_vViewSize, pos))
            {
                continue;
            }

            /*
             * Then find the entity somebody would have clicked on.
             */
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
        }
    }
    

    public ClickableHandler(Engine engine0)
    {
        _engine = engine0;
    }
}