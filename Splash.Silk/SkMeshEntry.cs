namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        public VertexArrayObject<float, uint> SkMesh; 

        public override bool IsMeshUploaded()
        {
            return SkMesh != null;
            // return RlMesh.vaoId != 0;
            // return false;
        }

        public SkMeshEntry(engine.joyce.Mesh jMesh)
            : base(jMesh)
        {
            SkMesh = null;
            //RlMesh.vaoId = 0;
        }
    }
}