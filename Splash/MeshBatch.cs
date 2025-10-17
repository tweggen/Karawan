using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Numerics;
using engine.joyce.components;

namespace Splash;



public class MeshBatch
{
    public readonly AMeshEntry AMeshEntry;

    public readonly Dictionary<AnimationsBatchKey, AnimationBatch> AnimationBatches = new();
    public List<AnimationBatch> ListAnimationBatches;
    
    public int NMeshes = 0;
    public Vector3 SumOfPositions = Vector3.Zero;

    private Flags.AnimBatching _animBatching;
    
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


    public void Add(
        in AAnimationsEntry? aAnimationsEntry, GPUAnimationState cGpuAnimationState, 
        in Matrix4x4 matrix, uint frameno,
        in FrameStats frameStats)
    {
        AnimationBatch animationBatch;
        if ((_animBatching & Flags.AnimBatching.ByAnimation) == 0)
        {
            cGpuAnimationState.ModelAnimation = null;
        }

        if ((_animBatching & Flags.AnimBatching.ByFrameno) == 0)
        {
            cGpuAnimationState.ModelAnimationFrame = 0;
        }
        
        AnimationsBatchKey key = new(aAnimationsEntry, cGpuAnimationState);
        AnimationBatches.TryGetValue(key, out animationBatch);
        if (null == animationBatch)
        {
            animationBatch = new AnimationBatch(aAnimationsEntry)
            {
                GpuAnimationState = cGpuAnimationState
            };
            AnimationBatches.Add(key, animationBatch);
            frameStats.NAnimations++;
        }
        
        /*
         * Now we can add our matrix to the list of matrices.
         */
        animationBatch.Matrices.Add(matrix);
        animationBatch.FrameNos.Add(frameno);
    }
    
    
    public MeshBatch(in AMeshEntry aMeshEntry,
        Flags.AnimBatching animBatching)
    {
        AMeshEntry = aMeshEntry;
        _animBatching = animBatching;
    }

}
