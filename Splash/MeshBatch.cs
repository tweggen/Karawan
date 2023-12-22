using System;
using System.Collections.Generic;
using System.Numerics;

namespace Splash
{
    public class MeshBatch
    {
        public readonly AMeshEntry AMeshEntry;
        public readonly List<Matrix4x4> Matrices = new();

        
        public Vector3 SumOfPositions = Vector3.Zero;
        public Vector3 AveragePosition
        {
            get => Matrices.Count > 0 ? SumOfPositions / Matrices.Count : Vector3.Zero;
        }


        public void AddMatrix(in Matrix4x4 m)
        {
            Matrices.Add(m);
        }
        
        
        public void Sort(Vector3 v3CameraPos)
        {
            if (Matrices != null && Matrices.Count > 0)
            {
                Matrices.Sort((b, a) =>
                {
                    float da = (a.Translation - v3CameraPos).LengthSquared();
                    float db = (b.Translation - v3CameraPos).LengthSquared();
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
            }
        }
        
        
        public MeshBatch(in AMeshEntry aMeshEntry)
        {
            AMeshEntry = aMeshEntry;
        }
    }
}
