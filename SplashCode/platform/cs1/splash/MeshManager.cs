using DefaultEcs;
using DefaultEcs.Resource;
using System;
using System.Collections.Generic;
using engine;
using static engine.Logger;


namespace Karawan.platform.cs1.splash
{
   
    public class MeshManager : AResourceManager<engine.joyce.Mesh, AMeshEntry>
    {
        private readonly engine.Engine _engine;
        private readonly IThreeD _threeD;

        public void FillMeshEntry(in AMeshEntry aMeshEntry)
        {
            _threeD.UploadMesh(aMeshEntry);
        }

        protected override AMeshEntry Load(engine.joyce.Mesh jMesh)
        {
            return _threeD.CreateMeshEntry(jMesh);
        }

        protected override void OnResourceLoaded(in Entity entity, engine.joyce.Mesh info, AMeshEntry aMeshEntry)
        {
            entity.Set<components.PfMesh>(new components.PfMesh(aMeshEntry));
        }

        protected override unsafe void Unload(engine.joyce.Mesh jMesh, AMeshEntry aMeshEntry)
        {
            _threeD.DestroyMeshEntry(aMeshEntry);
            base.Unload(jMesh, aMeshEntry);
        }


        public MeshManager(in engine.Engine engine, in IThreeD threeD)
        {
            _engine = engine;
            _threeD = threeD;
        }
    }
}
