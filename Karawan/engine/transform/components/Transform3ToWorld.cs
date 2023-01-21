using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Transform3ToWorld
    {
        public bool IsTotalVisible;
        public Matrix4x4 Matrix;

        public Transform3ToWorld( 
            bool isTotalVisible,
            in Matrix4x4 matrix 
            )
        {
            IsTotalVisible = isTotalVisible;
            Matrix = matrix;
        }
    }
}
