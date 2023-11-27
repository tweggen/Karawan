using System.Net.Security;
using System.Numerics;

namespace engine.joyce.components
{
    public struct Transform3ToParent
    {
        public bool IsVisible;
        public uint CameraMask;
        public Matrix4x4 Matrix;

        public Transform3 Transform3
        {
            //private get default;
            set {
                var mScale = Matrix4x4.CreateScale(value.Scale);
                var mRotate = Matrix4x4.CreateFromQuaternion(value.Rotation);
                var mTranslate = Matrix4x4.CreateTranslation(value.Position);
                var mToParent = mScale * mRotate * mTranslate;
                Matrix = mToParent;
            }
        }

        public override string ToString()
        {
            return $"IsVisible={IsVisible}, CameraMask={CameraMask:X}, Matrix={Matrix}";
        }


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


        public Transform3ToParent(bool isVisible, uint cameraMask, in Matrix4x4 matrix)
        {
            IsVisible = isVisible;
            CameraMask = cameraMask;
            Matrix = matrix;
        }
    }
}
