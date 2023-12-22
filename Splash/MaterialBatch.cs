using System;
using System.Collections.Generic;
using System.Numerics;

namespace Splash;


public class MaterialBatch
{
    public AMaterialEntry AMaterialEntry;
    public Dictionary<AMeshEntry, MeshBatch> MeshBatches;
    public List<MeshBatch> ListMeshBatches = null;

    public int NMeshes = 0;
    public Vector3 SumOfPositions = Vector3.Zero;
    public Vector3 AveragePosition
    {
        get => NMeshes > 0 ? SumOfPositions / NMeshes : Vector3.Zero;
    }


    public void Sort(Vector3 v3CameraPos)
    {
        if (MeshBatches != null && MeshBatches.Count > 0)
        {
            ListMeshBatches = new(MeshBatches.Values);
            ListMeshBatches.Sort((a, b) =>
            {
                float da = (a.AveragePosition - v3CameraPos).LengthSquared();
                float db = (b.AveragePosition - v3CameraPos).LengthSquared();
                if (da < db)
                {
                    return -1;
                }
                else if (da > db)
                {
                    return 1;
                }

                return 0;
            });
            
            
            /*
             * We sort a maximum of 10 closest meshes.
             */
            int sortMax = 10;
            foreach (var mb in ListMeshBatches)
            {
                if (--sortMax < 0)
                {
                    break;
                }

                mb.Sort(v3CameraPos);
            }

        }
        else
        {
            ListMeshBatches = null;
        }
    }
    
    
    public MaterialBatch(in AMaterialEntry aMaterialEntry)
    {
        AMaterialEntry = aMaterialEntry;
        MeshBatches = new();
    }
}