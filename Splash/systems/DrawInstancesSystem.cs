using System;
using System.Numerics;
using System.Collections.Generic;
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
                int nMaterialIndices = pfInstance.MeshMaterials.Count;
                int nMaterials = pfInstance.AMaterialEntries.Count;
                for (int i = 0; i < l; ++i)
                {
                    AMeshEntry aMeshEntry = pfInstance.AMeshEntries[i];
                    AMaterialEntry aMaterialEntry = null;

                    if (i < nMaterialIndices)
                    {
                        int materialIndex = pfInstance.MeshMaterials[i];
                        if (materialIndex < nMaterials)
                        {
                            aMaterialEntry = pfInstance.AMaterialEntries[materialIndex];
                        }
                        else
                        {
                            Error($"Invalid material index (> nMaterials=={nMaterials}");
                            continue;
                        }
                    }
                    else
                    {
                        Error($"Invalid index (>= nMaterialIndices=={nMaterialIndices}");
                        continue;
                    }

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
