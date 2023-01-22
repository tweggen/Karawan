using Raylib_CsLo;
using System.Collections.Generic;


namespace Karawan.platform.cs1.splash
{
    class MeshManager
    {

        private Dictionary<engine.joyce.Mesh, splash.RlMeshEntry> _dictMeshes;

        public unsafe splash.RlMeshEntry FindRlMesh(engine.joyce.Mesh jMesh)
        {
            splash.RlMeshEntry rlMeshEntry;
            if (_dictMeshes.TryGetValue(jMesh, out rlMeshEntry))
            {
            }
            else
            {
                MeshGenerator.CreateRaylibMesh(jMesh, out rlMeshEntry);
                fixed (Raylib_CsLo.Mesh* pRlMeshEntry = &rlMeshEntry.RlMesh)
                {
                    Raylib.UploadMesh(pRlMeshEntry, false);
                }
                _dictMeshes.Add(jMesh, rlMeshEntry);
            }
            return rlMeshEntry;
        }

        public MeshManager()
        {
            _dictMeshes = new();
        }
    }
}
