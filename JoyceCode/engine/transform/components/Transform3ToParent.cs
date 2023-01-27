using System.Net.Security;
using System.Numerics;

namespace engine.transform.components
{
    struct Transform3ToParent
    {
        public bool IsVisible;
        public uint CameraMask;
        public Matrix4x4 Matrix;

        public Transform3ToParent( bool isVisible, in Matrix4x4 matrix )
        {
            IsVisible = isVisible;
            CameraMask = 0xffffffff;
            Matrix = matrix;
        }

        public Transform3ToParent(bool isVisible, uint cameraMask, in Matrix4x4 matrix)
        {
            IsVisible = isVisible;
            CameraMask = cameraMask;
            Matrix = matrix;
        }
    }
}
