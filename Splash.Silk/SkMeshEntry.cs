using System.Net;
using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        public VertexArrayObject vao = null;
        private bool  _isUploaded = false;

        public float[] Vertices;
        public float[] Normals;
        public float[] UVs;
        public ushort[] Indices;

        private static int _nMeshes = 0;

        private bool _traceMesh = false;

        /**
         * Upload the mesh to the GPU.
         */
        public void Upload(in GL gl)
        {
            vao = new VertexArrayObject(gl, this);
            if (_traceMesh) Trace($"Uploaded Mesh vaoId={vao.Handle}");
            ++_nMeshes;
            if (_nMeshes > 2000)
            {
                Warning($"Uploaded more than 2000 meshes.");
            }

            _isUploaded = true;
        }

        /**
         * Release the ressources creaeted by Upload again.
         */
        public void Release(in GL gl)
        {
            _isUploaded = false;
            if (_traceMesh) Trace($"Releasing Mesh vaoId={vao.Handle}");
            vao.Dispose();
            vao = null;
            --_nMeshes;
        }

        public override bool IsUploaded()
        {
            if (vao != null)
            {
                if (!_isUploaded)
                {
                    Trace($"Boom vaoId={vao.Handle}");
                }
            }

            return _isUploaded == true;
        }

        public SkMeshEntry(engine.joyce.Mesh jMesh)
            : base(jMesh)
        {
        }
    }
}