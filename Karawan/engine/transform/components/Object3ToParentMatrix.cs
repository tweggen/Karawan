using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Object3ToParentMatrix
    {
        public Matrix4x4 Matrix;

        public Object3ToParentMatrix( in Matrix4x4 matrix )
        {
            Matrix = matrix;
        }
    }
}
