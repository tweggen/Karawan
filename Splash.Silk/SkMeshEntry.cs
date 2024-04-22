using System.Net;
using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk
{
    public class SkMeshEntry : AMeshEntry
    {
        public GL _gl;
        public VertexArrayObject vao = null;
        private bool  _isUploaded = false;

        public float[] Vertices = null;
        public float[] Normals = null;
        public float[] UVs = null;
        public ushort[] Indices = null;

        private static int _nMeshes = 0;

        private bool _traceMesh = false;
        
        
        /**
         * Upload the mesh to the GPU.
         */
        public void Upload()
        {
            vao = new VertexArrayObject(_gl, this);
            if (_traceMesh) Trace($"Uploaded Mesh vaoId={vao.Handle}");
            ++_nMeshes;
            if (_nMeshes > 5000)
            {
                Warning($"Uploaded {_nMeshes} more than 2000 meshes.");
            }

            _isUploaded = true;
        }


        public void Release()
        {
            _isUploaded = false;
            if (_traceMesh) Trace($"Releasing Mesh vaoId={vao.Handle}");
            vao.Dispose();
            vao = null;
            --_nMeshes;
        }
        

        /**
         * Release the ressources creaeted by Upload again.
         */
        public override void Dispose()
        {
            // TXWTODO: Also free arrays explicitely here?
            if (_isUploaded)
            {
                Release();
            }
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


        public override bool IsFilled()
        {
            return Vertices != null;
        }
        

        public SkMeshEntry(GL gl, AMeshParams aMeshParams)
            : base(aMeshParams)
        {
            _gl = gl;
        }
    }
}