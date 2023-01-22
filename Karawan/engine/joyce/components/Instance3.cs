using System.Collections;
using System.Numerics;

namespace Karawan.engine.joyce.components
{
    struct Instance3
    {
        // public Matrix4x4 PreTransform;
        public IList Meshes;
        public IList MeshMaterials;
        public IList Materials;

        public Instance3(joyce.Mesh mesh)
        {
            // PreTransform = Matrix4x4.Identity;

            Meshes = new joyce.Mesh[1];
            MeshMaterials = new int[1];
            Materials = new joyce.Material[1];

            Meshes[0] = mesh;
            MeshMaterials[0] = 0;
            Materials[0] = null;
        }
    }
}
