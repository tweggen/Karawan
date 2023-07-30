using System;
using System.Numerics;
using System.Collections.Generic;

namespace engine.joyce.components
{
    public struct Instance3
    {
        public Matrix4x4 ModelTransform;
        public IList<engine.joyce.Mesh> Meshes;
        public IList<int> MeshMaterials;
        public IList<engine.joyce.Material> Materials;
        public IList<engine.joyce.MeshProperties> MeshProperties;

        public override string ToString()
        {
            return $"{base.ToString()}: "
                   + $"{ModelTransform}, "
                   + $"{(Meshes == null ? "null" : Meshes.Count)} meshes, "
                   + $"{(MeshMaterials == null ? "null" : MeshMaterials.Count)} mesh materials, "
                   + $"{(Materials == null ? "null" : Materials.Count)} materials.";
        }

        public Instance3(joyce.Mesh mesh)
        {
            ModelTransform = Matrix4x4.Identity;
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
            ModelTransform = instanceDesc.ModelTransform;
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
