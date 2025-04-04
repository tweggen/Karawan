﻿using System;
using System.Numerics;
using System.Collections.Generic;
using engine;
using engine.geom;
using engine.joyce;
using static engine.Logger;

namespace Splash.systems;

/**
 * Render the platform meshes.
 * 
 * Groups by material and mesh.
 */
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(Splash.components.PfInstance))]
sealed class DrawInstancesSystem : DefaultEcs.System.AEntitySetSystem<CameraOutput>
{
    private object _lo = new();
    private engine.Engine _engine;
    private IThreeD _threeD;

    private Vector3 _vCameraPos = new();
    private Plane _nearFrustum = new();
    private Plane _farFrustum = new();
    private Plane _leftFrustum = new();
    private Plane _rightFrustum = new();
    private Plane _topFrustum = new();
    private Plane _bottomFrustum = new();
    private int _nInstancesConsidered;
    private int _nInstancesAppended;
    
    private void _appendMeshRenderList(
        in CameraOutput cameraOutput,
        in ReadOnlySpan<DefaultEcs.Entity> entities
    )
    {
        foreach (var entity in entities)
        {
            var transform3ToWorld = entity.Get<engine.joyce.components.Transform3ToWorld>();
            if (transform3ToWorld.IsVisible && 0 != (transform3ToWorld.CameraMask & cameraOutput.CameraMask))
            {
                var pfInstance = entity.Get<Splash.components.PfInstance>();
                var id = pfInstance.InstanceDesc;
                _nInstancesConsidered++;

                Matrix4x4 mModelTotalTransform = id.ModelTransform * transform3ToWorld.Matrix; 
                Vector3 vPos = mModelTotalTransform.Translation;
                
                /*
                 * Per instance lod test
                 */
                if (Vector3.Distance(_vCameraPos, vPos) > id.MaxDistance)
                {
                    /*
                     * Too for away to render instance.
                     */
                    continue;
                }
                
                /*
                 * Only perform CPU culling for perspective projection cameras.
                 */
                if (cameraOutput.Camera3.Angle > 0f)
                {
                    /*
                     * Before adding the instance, let's look, if it is in front of the camera,
                     * inside the viewing cone.
                     *
                     * The AABB is in instance local coordinates.
                     */
                    AABB aabb = id.AABBMerged;
                    aabb.Transform(mModelTotalTransform);
                    if (aabb.SignedDistance(_nearFrustum) < 0) continue;
                    if (aabb.SignedDistance(_farFrustum) < 0) continue;
                    if (aabb.SignedDistance(_leftFrustum) < 0) continue;
                    if (aabb.SignedDistance(_rightFrustum) < 0) continue;
                    if (aabb.SignedDistance(_topFrustum) < 0) continue;
                    if (aabb.SignedDistance(_bottomFrustum) < 0) continue;
                }

                engine.joyce.components.AnimationState cAnimationState;
                if (entity.Has<engine.joyce.components.AnimationState>())
                {
                    cAnimationState = entity.Get<engine.joyce.components.AnimationState>();
                }
                else
                {
                    cAnimationState.ModelAnimation = null;
                    cAnimationState.ModelAnimationFrame = 0;
                }
                
                _nInstancesAppended++;
                cameraOutput.AppendInstance(pfInstance, transform3ToWorld.Matrix, cAnimationState);
            }
        }
    }

    private void _createPlane(float x, float y, float z, float w, out Plane plane)
    {
        Vector3 vNormal = new Vector3(x, y, z);
        float distance = w;
        float l = vNormal.Length();
        vNormal /= l;
        distance /= l;
        plane = new (vNormal, distance);
    }


    protected override void PreUpdate(CameraOutput cameraOutput)
    {
        cameraOutput.Camera3.GetViewMatrix(out var mView, cameraOutput.TransformToWorld);
        cameraOutput.Camera3.GetProjectionMatrix(out var mProjection,new Vector2(1f, 1f));

        _vCameraPos = cameraOutput.TransformToWorld.Translation;
        
        var mViewProj = mView * mProjection;
        
        /*
         * Before the update, compute the frustrum planes.
         */
        _createPlane(mViewProj.M13, mViewProj.M23, mViewProj.M33, mViewProj.M43, out _nearFrustum);
        _createPlane(mViewProj.M14-mViewProj.M13, mViewProj.M24-mViewProj.M23, mViewProj.M34-mViewProj.M33, mViewProj.M44-mViewProj.M43, out _farFrustum);
        _createPlane(mViewProj.M14+mViewProj.M11, mViewProj.M24+mViewProj.M21, mViewProj.M34+mViewProj.M31, mViewProj.M44+mViewProj.M41, out _leftFrustum);
        _createPlane(mViewProj.M14-mViewProj.M11, mViewProj.M24-mViewProj.M21, mViewProj.M34-mViewProj.M31, mViewProj.M44-mViewProj.M41, out _rightFrustum);
        _createPlane(mViewProj.M14-mViewProj.M12, mViewProj.M24-mViewProj.M22, mViewProj.M34-mViewProj.M32, mViewProj.M44-mViewProj.M42, out _topFrustum);
        _createPlane(mViewProj.M14+mViewProj.M12, mViewProj.M24+mViewProj.M22, mViewProj.M34+mViewProj.M32, mViewProj.M44+mViewProj.M42, out _bottomFrustum);

        _nInstancesAppended = 0;
        _nInstancesConsidered = 0;
    }

    protected override void PostUpdate(CameraOutput cameraOutput)
    {
        /*
         * Now that we computed a single frame, we need to do some post processing,
         * that is sorting the batches for transparent rendering.
         */
        cameraOutput.ComputeAfterAppend();
    }


    protected override void Update(CameraOutput cameraOutput, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        _appendMeshRenderList(cameraOutput, entities);
    }


    public DrawInstancesSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
        _threeD = I.Get<IThreeD>();
    }
}
