using System.Net.Security;
using System.Numerics;

namespace engine.transform.components
{
    public struct Transform3ToParent
    {
        public bool IsVisible;
        public uint CameraMask;
        public Matrix4x4 Matrix;

        public void GetFront( out Vector3 front)
        {
            front.X = -Matrix.M13;
            front.Y = -Matrix.M23;
            front.Z = -Matrix.M33;
        }

        public void GetUp(out Vector3 up)
        {
            up.X = Matrix.M12;
            up.Y = Matrix.M22;
            up.Z = Matrix.M32;
        }

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
