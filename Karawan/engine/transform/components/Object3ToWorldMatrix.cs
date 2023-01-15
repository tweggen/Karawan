using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Object3ToWorldMatrix
    {
        public Matrix4x4 Matrix;

        public Object3ToWorldMatrix( in Matrix4x4 matrix )
        {
            Matrix = matrix;
        }
    }
}
