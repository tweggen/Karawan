using DefaultEcs;
using DefaultEcs.Resource;
using Raylib_CsLo;
using System.Collections.Generic;


namespace Karawan.platform.cs1.splash
{
    class MeshManager : AResourceManager<engine.joyce.Mesh, RlMeshEntry>
    {

//        private Dictionary<engine.joyce.Mesh, splash.RlMeshEntry> _dictMeshes;


        protected unsafe override RlMeshEntry Load(engine.joyce.Mesh jMesh)
        {
            RlMeshEntry rlMeshEntry;
            MeshGenerator.CreateRaylibMesh(jMesh, out rlMeshEntry);
            fixed (Raylib_CsLo.Mesh* pRlMeshEntry = &rlMeshEntry.RlMesh)
            {
                Raylib.UploadMesh(pRlMeshEntry, false);
            }
            return rlMeshEntry;
        }

        protected override void OnResourceLoaded(in Entity entity, engine.joyce.Mesh info, RlMeshEntry rlMeshEntry)
        {
            entity.Set<components.RlMesh>(new components.RlMesh(rlMeshEntry));
        }

        protected override unsafe void Unload(engine.joyce.Mesh jMesh, RlMeshEntry rlMeshEntry)
        {
            Raylib.UnloadMesh(rlMeshEntry.RlMesh);
            base.Unload(jMesh, rlMeshEntry);
        }

#if false
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
#endif

        public MeshManager()
        {
//            _dictMeshes = new();
        }
    }
}
