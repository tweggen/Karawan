using System.Numerics;

namespace engine.transform.components
{
    struct Transform3ToParent
    {
        public bool IsVisible;
        public Matrix4x4 Matrix;

        public Transform3ToParent( bool isVisible, in Matrix4x4 matrix )
        {
            IsVisible = isVisible;
            Matrix = matrix;
        }
    }
}
