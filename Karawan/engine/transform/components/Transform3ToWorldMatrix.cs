using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Transform3ToWorldMatrix
    {
        public bool IsTotalVisible;
        public Matrix4x4 Matrix;

        public Transform3ToWorldMatrix( 
            bool isTotalVisible,
            in Matrix4x4 matrix 
            )
        {
            IsTotalVisible = isTotalVisible;
            Matrix = matrix;
        }
    }
}
