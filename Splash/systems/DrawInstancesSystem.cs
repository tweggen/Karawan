using System;
using System.Numerics;
using System.Collections.Generic;
using engine.geom;
using static engine.Logger;

namespace Splash.systems;

/**
 * Render the platform meshes.
 * 
 * Groups by material and mesh.
 */
[DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(Splash.components.PfInstance))]
sealed class DrawInstancesSystem : DefaultEcs.System.AEntitySetSystem<CameraOutput>
{
    private object _lo = new();
    private engine.Engine _engine;
    private IThreeD _threeD;

    private Plane _nearFrustum = new();
    private int _nInstancesConsidered;
    private int _nInstancesAppended;
    

    private void _appendMeshRenderList(
        in CameraOutput cameraOutput,
        in ReadOnlySpan<DefaultEcs.Entity> entities
    )
    {
        foreach (var entity in entities)
        {
            var transform3ToWorld = entity.Get<engine.transform.components.Transform3ToWorld>();
            if (0 != (transform3ToWorld.CameraMask & cameraOutput.CameraMask))
            {
                var pfInstance = entity.Get<Splash.components.PfInstance>();
                _nInstancesConsidered++;
                
                /*
                 * Before adding the instance, let's look, if it is in front of the camera,
                 * inside the viewing cone.
                 *
                 * The AABB is in world coordinates. 
                 */
                AABB aabb = pfInstance.InstanceDesc.Aabb;
                float sd = aabb.SignedDistance(_nearFrustum);
                if (sd < 0) continue;
                _nInstancesAppended++;
                cameraOutput.AppendInstance(pfInstance, transform3ToWorld.Matrix);
            }
        }
    }


    protected override void PreUpdate(CameraOutput cameraOutput)
    {
        cameraOutput.Camera3.GetViewMatrix(out var mView, cameraOutput.TransformToWorld);
        cameraOutput.Camera3.GetProjectionMatrix(out var mProjection,new Vector2(1f, 1f));

        var mViewProj = mView * mProjection;
        
        /*
         * Before the update, compute the near frustrum plane.
         */
        Vector3 vNormal = new Vector3(
            mViewProj.M13,
            mViewProj.M23,
            mViewProj.M33
        ); 
        float distance = mViewProj.M43;
        float l = vNormal.Length();
        vNormal /= l;
        distance /= l;
        _nearFrustum = new (vNormal, distance);

        _nInstancesAppended = 0;
        _nInstancesConsidered = 0;
        // Trace($"{_nearFrustum}");
    }

    protected override void PostUpdate(CameraOutput cameraOutput)
    {
        Trace($"Camera {cameraOutput.CameraMask}: appended {_nInstancesAppended} out of considered {_nInstancesConsidered}");
    }


    protected override void Update(CameraOutput cameraOutput, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        _appendMeshRenderList(cameraOutput, entities);
    }


    public DrawInstancesSystem(
        engine.Engine engine,
        IThreeD threeD
    )
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _threeD = threeD;
    }
}
