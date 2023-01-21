using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Transform3ToParentMatrix
    {
        public bool IsVisible;
        public Matrix4x4 Matrix;

        public Transform3ToParentMatrix( bool isVisible, in Matrix4x4 matrix )
        {
            IsVisible = isVisible;
            Matrix = matrix;
        }
    }
}
