using System;
using System.Numerics;


namespace engine.joyce.components
{
    public struct Camera3
    {
        public float Angle;
        public float NearFrustum;
        public float FarFrustum;
        public uint CameraMask;
        
        
        public override string ToString()
        {
            return $"{base.ToString()}, Angle={Angle}, NearFrustum={NearFrustum}, NearFrustum={NearFrustum}, CameraMask={CameraMask:X}";
        }
        

        public void GetViewMatrix(out Matrix4x4 matView, in Matrix4x4 mCameraToWorld)
        {
            var vCameraPosition = mCameraToWorld.Translation;
            Vector3 vY;
            Vector3 vUp = vY = new Vector3(mCameraToWorld.M21, mCameraToWorld.M22, mCameraToWorld.M23);
            Vector3 vZ = new Vector3(-mCameraToWorld.M31, -mCameraToWorld.M32, -mCameraToWorld.M33);
            Vector3 vFront = -vZ;
            Vector3 vTarget = vCameraPosition + vFront;
            matView = Matrix4x4.CreateLookAt(vCameraPosition, vCameraPosition+vZ , vUp);
        }

        
        public void GetProjectionMatrix(out Matrix4x4 matProjection, in Vector2 vViewSize)
        {
            float right = NearFrustum * (float)Math.Tan(Angle * 0.5f * (float)Math.PI / 180f);
            float invAspect = vViewSize.Y / vViewSize.X;
            float top = right * invAspect;

            float n = NearFrustum;
            float f = FarFrustum;
            float l = -right;
            float r = right;
            float t = top;
            float b = -top;
            Matrix4x4 m = new(
                2f * n / (r - l), 0f, (r+l)/(r-l), 0f,
                0f, 2f * n / (t - b), (t+b)/(t-b), 0f,
                0f, 0f, -(f + n) / (f - n), -2f * f * n / (f - n),
                0f, 0f, -1f, 0f
            );

            // TXWTODO: We need a smarter way to fix that to the view.
            Matrix4x4 mScaleToViewWindow =  Matrix4x4.Identity;
            matProjection = Matrix4x4.Transpose(m*mScaleToViewWindow);
        }
    }
}
