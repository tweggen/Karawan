using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using BepuPhysics.Collidables;
using builtin.controllers;
using engine.joyce.components;
using engine.news;
using engine.physics;
using static engine.Logger;

namespace engine.news;


class ClickResult
{
    public DefaultEcs.Entity Entity;
    public Vector2 RelPos;
    public float Z;
}


/**
 * Scan all clickable objects for the object the user might have clicked on.
 *
 * There are three types of clickable object
 * - inside OSD object framebuffer
 * - 2d isometrically projected (use boundary boxes)
 * - 3d projected (with physics awareness)
 */
public class ClickableHandler
{
    private engine.Engine _engine;
    private Camera3 _cCamera3;
    private joyce.components.Transform3ToWorld _cCamTransform;
    private Vector2 _vViewSize;
    private Matrix4x4 _mProjection;
    private Matrix4x4 _mView;


    private FingerStateHandler _fingerStateHandler;

    
    private void _findAt(in Vector2 v2EvPos, in Vector2 v2EvSize, in List<ClickResult> listResults)
    {
        /*
         * We have two (+, see above) different version of Clickables: Those from 3d space
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

        /*
         * Iterate though all clickable entities by AABB boxes.
         *
         * This should catch the framebuffer or similar concepts.
         */
        foreach (var entity in clickableEntities)
        {
            var cTransform = entity.Get<joyce.components.Transform3ToWorld>();
            // if (cTransform.Matrix.Translation.Z >= minZ) continue;
            // minZ = cTransform.Matrix.Translation.Z;
            
            /*
             * Is it visible by the camera we are looking for?
             */
            if (0 == (cTransform.CameraMask & _cCamera3.CameraMask)) continue;
            
            /*
             * Now we can transform the AABB into viewable space and look if pos is inside.
             */

            joyce.InstanceDesc id = entity.Get<Instance3>().InstanceDesc;

            Matrix4x4 mTransformView = cTransform.Matrix * _mView;
            Matrix4x4 mTransformViewProjection = mTransformView * _mProjection;
            /*
             * Transform the aabb to screenspace opengl coordinates, i.e. -1 ... 1
             */
            Vector4 vAA4 = Vector4.Transform(id.AABBTransformed.AA, mTransformViewProjection);
            Vector4 vBB4 = Vector4.Transform(id.AABBTransformed.BB, mTransformViewProjection);
            // TXWTODO: In addition, consider UL and LR from the actual Camera3 data structure.
            
            /*
             * Scale it up to screen space coordinates.
             */
            Vector2 vAA2;
            Vector2 vBB2;

            {
                _cCamera3.ScreenExtent(v2EvSize, out var v2ScrUl, out var v2ScrLr);
                Vector2 size = v2ScrLr-v2ScrUl;
                vAA2 = v2ScrUl + new Vector2(
                    (vAA4.X / vAA4.W + 1f) * size.X / 2f,
                    (-vAA4.Y / vAA4.W + 1f) * size.Y / 2f);
                vBB2 = v2ScrUl + new Vector2(
                    (vBB4.X / vBB4.W + 1f) * size.X / 2f,
                    (-vBB4.Y / vBB4.W + 1f) * size.Y / 2f);
                
                /*
                 * Note: if this is the 2d display canvas for the osd, vAA2 and vBB2 result
                 * in a 16:9 extent within the (-1;-1)-(1:1) range.
                 */
            }

            Vector2 ul = Vector2.Min(vAA2, vBB2);
            Vector2 lr = Vector2.Max(vAA2, vBB2);
            
            // Trace($"pos is {pos} Transformed position is ul={ul}, lr={lr}");

            /*
             * Is it within the bounds of the AABB?
             * Then out and done!
             */
            if (v2EvPos.X >= ul.X && v2EvPos.X < lr.X && v2EvPos.Y >= ul.Y && v2EvPos.Y < lr.Y)
            {
                /*
                 * Now look, if we already found something that is closer.
                 */
                Vector3 v3Center = Vector3.Transform(id.AABBTransformed.Center, mTransformView);

                /*
                 * Now compute the relative position of the click with reference range of the
                 * object we click at. Check for non-zero size.
                 */
                Vector2 v2RelPos;

                if (ul.X < lr.X && ul.Y < lr.Y)
                {
                    /*
                     * This looks right at first glance, however, when used on the framebuffer canvas, this
                     * is wrong. The event comes in with a (-1;-1)-(1;1) range, while the visible extent
                     * is (-1;-0.57) - (1;0.57)
                     */
                    v2RelPos = new((v2EvPos.X - ul.X) / (lr.X - ul.X), (v2EvPos.Y - ul.Y) / (lr.Y - ul.Y));
                }
                else
                {
                    v2RelPos = Vector2.Zero;
                }

                /*
                 * This is a hit.
                 */
                // Trace($"Clickable {entity} was clicked.");
                listResults.Add(new ClickResult()
                {
                    Entity = entity,
                    RelPos = v2RelPos,
                    Z = v3Center.Z,
                });
            }
        }
    }


    private void _updateFromCamera(DefaultEcs.Entity eCamera)
    {
        _cCamera3 = eCamera.Get<Camera3>();
        _cCamTransform = eCamera.Get<joyce.components.Transform3ToWorld>();
        _cCamera3.GetViewSize(out _vViewSize);
        _cCamera3.GetProjectionMatrix(out _mProjection, _vViewSize);
        _cCamera3.GetViewMatrix(out _mView, _cCamTransform.Matrix);
    }


    private void _onMainCameraRayHit(
        CollidableReference collidableReference,
        CollisionProperties collisionProperties,
        float t,
        Vector3 vNormal)
    {
        if (null != collisionProperties)
        {
            Trace($"Collision with {collisionProperties.Name}");
        }
    }
    
    
    /**
     * OnClick is called with an INPUT_LOGICAL_XXX event.
     * We consider the fields:
     * - PhysicalSize: Excerpt the viewSize for ???
     * - PHysicalPosition: Excerpt the position of the click.
     *
     * To understand what we clicked on and if we did, we ask the camera component
     * for its view size and if the position as described by the physicalposition fits
     * on the screen.
     * // TXWTODO: How can that be an indicator if it fits on screen? viewSize and PhysicalSize of the event would need to correlate in the first place.
     *
     * In the end, we just need to check if the click event is inside the logical screen of the camera.
     * THen, we need to compute the relative position with respect to the clickable object, passing
     * on the event.
     *
     * However, click event source and camera are basically unrelated. So the camera needs to know
     * its extent relative to the physical screen, and the click event needs to be related to the
     * physical screen.
     * 
     */
    public void OnClick(engine.news.Event ev)
    {
        Debug.Assert(ev.Type == Event.INPUT_LOGICAL_PRESSED, "Expecting INPUT_LOGICAL_PRESSED event.");

        // TXWTODO: So let's please find all locations we assume a square pixel size in this context.

        /*
         * Now iterate through all cameras.
         * We need a copy because event handlers shall be able to create/remove entities.
         * TXWTODO: This is at most 32 (plus dup'ped cams), why not sort it? So that we're
         * having a defined order?
         */
        var cameras = new List<DefaultEcs.Entity>(_engine.GetEcsWorld().GetEntities()
            .With<Camera3>()
            .With<joyce.components.Transform3ToWorld>()
            .AsEnumerable());
        foreach (var eCamera in cameras)
        {
            _updateFromCamera(eCamera);

            Vector2 v2EvSize = ev.PhysicalSize;
            Vector2 v2EvPos = ev.PhysicalPosition;

            /*
             * First check if the camera view is under that position.
             * This checks for the selected camera window. Coordinate space
             * used is event size coordinates.
             *
             * This method assumes, the v2EvSize matches the possible screen size.
             */
            if (!_cCamera3.ContainsScreenPosition(v2EvSize, v2EvPos))
            {
                continue;
            }

            List<ClickResult> clickResults = new();

            /*
             * Then find the entity somebody would have clicked on.
             */
            _findAt(v2EvPos, v2EvSize, in clickResults);
            clickResults.Sort((a, b) => -a.Z.CompareTo(b.Z));
            foreach (var cr in clickResults)
            {
                // TXWTODO: We better should set the event to IsHandled here.
                var eFound = cr.Entity;
                var v2RelPos = cr.RelPos;
                var cClickable = eFound.Get<engine.behave.components.Clickable>();

                _fingerStateHandler.OnFingerPressed(ev, ev =>
                    new ClickableFingerState(ev.PhysicalPosition, eFound, cClickable, v2RelPos));

                if (ev.IsHandled)
                {
                    break;
                }
            }
        }

        /*
         * Finally, using the real cam (that we just define to be the main view), to do raycasting.
         * This, however, will consume the input event, so no other actions, like panning or moving,
         * would be performed at all.
         */

        if (false && _engine.Camera.TryGet(out var eMainCamera))
        {
            ref var cCamTransform = ref eMainCamera.Get<engine.joyce.components.Transform3ToWorld>();
            ref var cCamera = ref eMainCamera.Get<engine.joyce.components.Camera3>();
            ref var mCameraToWorld = ref cCamTransform.Matrix;
            Vector3 vX = new Vector3(mCameraToWorld.M11, mCameraToWorld.M12, mCameraToWorld.M13);
            Vector3 vY = new Vector3(mCameraToWorld.M21, mCameraToWorld.M22, mCameraToWorld.M23);
            Vector3 vZ = new Vector3(mCameraToWorld.M31, mCameraToWorld.M32, mCameraToWorld.M33);
            float dist = vZ.Length();
            var vCamPosition = mCameraToWorld.Translation;

            /*6
             * The virtual camera screen is "at" NearFrustum.
             * However, for raycasting, the actual position (distance to the camera) does not matter,
             * any direction is ok.
             *
             * The extent of the screen is defined by the angle of the projection,
             */
            float xExtent = 2f * Single.Tan((cCamera.Angle * Single.Pi / 180f) / 2f) * dist;
            float yExtent = xExtent * 9f / 16f;
            float mainHeight = _vViewSize.X * 9f / 16f;
            float mainTop = (_vViewSize.Y - mainHeight) / 2f;

            Vector2 v2Pos = new Vector2(ev.PhysicalPosition.X / _vViewSize.X - 0.5f,
                (ev.PhysicalPosition.Y - mainTop) / mainHeight - 0.5f);
            Trace($"v2Pos = {v2Pos}, xExtent = {xExtent}, yExtent = {yExtent}");
            Vector3 v3Target = -vZ + vX * xExtent * v2Pos.X - vY * yExtent * v2Pos.Y;
            I.Get<engine.physics.API>()
                .RayCast(vCamPosition, v3Target, cCamera.FarFrustum, _onMainCameraRayHit);
        }

    }


    public void OnRelease(engine.news.Event ev)
    {
        Debug.Assert(ev.Type == Event.INPUT_LOGICAL_RELEASED, "Expecting INPUT_LOGICAL_RELEASED event.");
        _fingerStateHandler.OnFingerReleased(ev);
    }
    

    public ClickableHandler(Engine engine0)
    {
        _engine = engine0;
        _fingerStateHandler = new();
    }
}