namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        // public Raylib_CsLo.Mesh RlMesh;

        public override bool IsMeshUploaded()
        {
            // return RlMesh.vaoId != 0;
            return false;
        }

        public SkMeshEntry(engine.joyce.Mesh jMesh)
            : base(jMesh)
        {
            //RlMesh.vaoId = 0;
        }
    }
}