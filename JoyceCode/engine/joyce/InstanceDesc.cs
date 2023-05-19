using System.Numerics;
using System.Collections.Generic;

namespace engine.joyce
{
    /**
     * Describe one specific instance of a 3d object (aka Instance3 components)
     * Note that this is only a descriptor, not a lifetime object.
     */
    public class InstanceDesc
    {
        public Matrix4x4 ModelTransform;
        public IList<engine.joyce.Mesh> Meshes;
        public IList<int> MeshMaterials;
        public IList<engine.joyce.Material> Materials;

        public InstanceDesc()
        {
            ModelTransform = Matrix4x4.Identity;
            Meshes = new List<Mesh>();
            MeshMaterials = new List<int>();
            Materials = new List<Material>();
        }

        public InstanceDesc(
            in IList<engine.joyce.Mesh> meshes,
            in IList<int> meshMaterials,
            in IList<engine.joyce.Material> materials
        )
        {
            ModelTransform = Matrix4x4.Identity;
            Meshes = meshes;
            MeshMaterials = meshMaterials;
            Materials = materials;
        }
    }
}
