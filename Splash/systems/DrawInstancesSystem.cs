using System;
using System.Numerics;
using System.Collections.Generic;

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

    private CameraOutput _cameraOutput = null;


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

                int l = pfInstance.AMeshEntries.Count;
                for (int i = 0; i < l; ++i)
                {
                    AMeshEntry aMeshEntry = pfInstance.AMeshEntries[i];
                    AMaterialEntry aMaterialEntry = pfInstance.AMaterialEntries[pfInstance.MeshMaterials[i]];

                    // Skip things that incompletely are loaded.
                    if (null == aMeshEntry)
                    {
                        continue;
                    }

                    if (null == aMaterialEntry)
                    {
                        aMaterialEntry = _threeD.GetDefaultMaterial();
                    }

                    var rMatrix = transform3ToWorld.Matrix;

                    cameraOutput.AppendInstance(aMeshEntry, aMaterialEntry, rMatrix);
                }
            }
        }
    }


    protected override void PreUpdate(CameraOutput cameraOutput)
    {
    }

    protected override void PostUpdate(CameraOutput cameraOutput)
    {
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
        _cameraOutput = null;
    }
}
