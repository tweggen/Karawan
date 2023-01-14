using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Object3ToWorldMatrix
    {
        Matrix4x4 Matrix;

        Object3ToWorldMatrix( Matrix4x4 matrix )
        {
            Matrix = matrix;
        }
    }
}
