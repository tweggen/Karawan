using System.Net;
using Silk.NET.OpenGL;

namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        public VertexArrayObject vao;

        public float[] Vertices;
        public float[] Normals;
        public float[] UVs;
        public uint[] Indices;

        public void Upload(in GL gl)
        {
            vao = new VertexArrayObject(gl, this);
        }
        
        public override bool IsMeshUploaded()
        {
            return vao != null;
        }

        public SkMeshEntry(engine.joyce.Mesh jMesh)
            : base(jMesh)
        {
            vao = null;
        }
    }
}