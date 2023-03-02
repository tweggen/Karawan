using DefaultEcs;
using DefaultEcs.Resource;
using Raylib_CsLo;
using System;
using System.Collections.Generic;


namespace Karawan.platform.cs1.splash
{
    
    public class MeshManager : AResourceManager<engine.joyce.Mesh, RlMeshEntry>
    {
        private readonly engine.Engine _engine;

        public unsafe void FillRlMeshEntry(in RlMeshEntry rlMeshEntry)
        {
            fixed (Raylib_CsLo.Mesh* pRlMeshEntry = &rlMeshEntry.RlMesh)
            {
                Raylib.UploadMesh(pRlMeshEntry, false);
            }
            Console.WriteLine($"MeshManager: Uploaded Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");
        }

        protected override RlMeshEntry Load(engine.joyce.Mesh jMesh)
        {
            RlMeshEntry rlMeshEntry;
            MeshGenerator.CreateRaylibMesh(jMesh, out rlMeshEntry);
            return rlMeshEntry;
        }

        protected override void OnResourceLoaded(in Entity entity, engine.joyce.Mesh info, RlMeshEntry rlMeshEntry)
        {
            entity.Set<components.RlMesh>(new components.RlMesh(rlMeshEntry));
        }

        protected override unsafe void Unload(engine.joyce.Mesh jMesh, RlMeshEntry rlMeshEntry)
        {
            _engine.QueueCleanupAction(() =>
            {
                Console.WriteLine($"MeshManager: Unloading Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");
                Raylib.UnloadMesh(rlMeshEntry.RlMesh);
            });
            base.Unload(jMesh, rlMeshEntry);
        }


        public MeshManager(in engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
