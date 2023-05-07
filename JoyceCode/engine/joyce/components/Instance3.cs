using System;
using System.Collections;
using System.Collections.Generic;

namespace engine.joyce.components
{
    public struct Instance3
    {
        // public Matrix4x4 PreTransform;
        public IList<engine.joyce.Mesh> Meshes;
        public IList<int> MeshMaterials;
        public IList<engine.joyce.Material> Materials;

        public override string ToString()
        {
            return $"{base.ToString()}: "
                   + $"{(Meshes == null ? "null" : Meshes.Count)} meshes, "
                   + $"{(MeshMaterials == null ? "null" : MeshMaterials.Count)} mesh materials, "
                   + $"{(Materials == null ? "null" : Materials.Count)} materials.";
        }

        public Instance3(joyce.Mesh mesh)
        {
            // PreTransform = Matrix4x4.Identity;

            Meshes = new joyce.Mesh[1];
            MeshMaterials = new int[1];
            Materials = new joyce.Material[1];

            Meshes[0] = mesh;
            MeshMaterials[0] = 0;
            Materials[0] = new Material();
        }

        /**
         * Construct a new instance3.
         * Caution: This uses the lists from the description.
         */
        public Instance3(in engine.joyce.InstanceDesc instanceDesc)
        {
            Meshes = instanceDesc.Meshes;
            MeshMaterials = instanceDesc.MeshMaterials;
            Materials = instanceDesc.Materials;
#if DEBUG
            for(int i=0; i<Materials.Count; ++i)
            {
                if(null == Materials[i])
                {
                    throw new ArgumentNullException($"Instance3: Material[{i}] is null.");
                }
            }
#endif
        }
    }
}
