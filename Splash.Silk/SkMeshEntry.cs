using System.Net;
using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        public VertexArrayObject vao;

        public float[] Vertices;
        public float[] Normals;
        public float[] UVs;
        public ushort[] Indices;

        /**
         * Upload the mesh to the GPU.
         */
        public void Upload(in GL gl)
        {
            vao = new VertexArrayObject(gl, this);
            Trace($"Uploaded Mesh vaoId={vao.Handle}, nVertices={Vertices.Length}");
        }

        /**
         * Release the ressources creaeted by Upload again.
         */
        public void Release(in GL gl)
        {
            Trace($"Releasing Mesh vaoId={vao.Handle}, nVertices={Vertices.Length}");
            vao.Dispose();
            vao = null;
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