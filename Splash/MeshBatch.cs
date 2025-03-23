using System;
using System.Collections.Generic;
using System.Numerics;
using engine.joyce.components;

namespace Splash;



public class MeshBatch
{
    public readonly AMeshEntry AMeshEntry;

    public readonly Dictionary<AAnimationsEntry, AnimationBatch> AnimationBatches = new();
    public List<AnimationBatch> ListAnimationBatches;
    
    public int NMeshes = 0;
    public Vector3 SumOfPositions = Vector3.Zero;

    public Vector3 AveragePosition
    {
        get => NMeshes > 0 ? SumOfPositions / NMeshes : Vector3.Zero;
    }

    public void Sort(Vector3 v3CameraPos, Vector3 v3CameraZ, float angleCamera)
    {
        if (AnimationBatches != null && AnimationBatches.Count > 0)
        {
            ListAnimationBatches = new(AnimationBatches.Values);
            ListAnimationBatches.Sort((b, a) =>
            {
                float da, db;
                if (angleCamera != 0f)
                {
                    da = -(a.AveragePosition - v3CameraPos).LengthSquared();
                    db = -(b.AveragePosition - v3CameraPos).LengthSquared();
                }
                else
                {
                    da = Vector3.Dot(a.AveragePosition - v3CameraPos, v3CameraZ);
                    db = Vector3.Dot(b.AveragePosition - v3CameraPos, v3CameraZ);
                }

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
            foreach (var mb in ListAnimationBatches)
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
            ListAnimationBatches = null;
        }
    }


    public MeshBatch(in AMeshEntry aMeshEntry)
    {
        AMeshEntry = aMeshEntry;
    }

}
