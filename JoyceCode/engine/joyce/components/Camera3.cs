using System;
using System.Numerics;


namespace engine.joyce.components;

public struct Camera3
{
    public enum Flags
    {
        PreloadOnly = 0x00000001,
        RenderSkyboxes = 0x00000008,
        DontRenderInstances = 0x00000010,
        RenderMapIcons = 0x00000020,
        DisableDepthTest = 0x00000040,
        EnableFog = 0x00000080
    };

    public float Angle = 60f;
    public float NearFrustum = 10f;
    public float FarFrustum = 1000f;
    
    /**
     * The upper left position on screen this one shall be rendered in.
     * 0,0 is upper left, 1,1 is lower right
     */
    public Vector2 UL = Vector2.Zero;
    
    /**
     * The lower right position on screen this one shall be rendered in.
     * 0,0 is upper left, 1,1 is lower right
     */
    public Vector2 LR = Vector2.One;

    /**
     * Scale the camera output. Values larger than one enlarging, values smaller
     * than one, well, shrinking. The use of negative values currently is undefined.
     */
    public float Scale = 1.0f;

    public uint CameraMask = 0x00000000;
    public Flags CameraFlags = 0x00000000;


    /**
     * A vector consisting of RGB fog + W distance.
     */
    public Vector4 Fog;
    
    /**
     * The renderbuffer this camera shall be rendered into.
     * If null, we shall use the main screen.
     */
    public engine.joyce.Renderbuffer Renderbuffer;
    

    public override string ToString()
    {
        return
            $"Angle={Angle}, NearFrustum={NearFrustum}, NearFrustum={NearFrustum}, CameraMask={CameraMask:X}, CameraFlags={CameraFlags:X}";
    }


    public bool ContainsScreenPosition(in Vector2 viewSize, in Vector2 cand)
    {
        var v2ScreenUL = new Vector2(viewSize.X * UL.X, viewSize.Y * UL.Y);
        if (cand.X < v2ScreenUL.X || cand.Y < v2ScreenUL.Y) return false;
        var v2ScreenLR = new Vector2(viewSize.X * LR.X, viewSize.Y * LR.Y);
        if (cand.X > v2ScreenLR.X || cand.Y > v2ScreenLR.Y) return false;
        return true;
    }
    

    public void ScreenExtent(in Vector2 viewSize, out Vector2 v2ScreenUL, out Vector2 v2ScreenLR)
    {
        v2ScreenUL = new Vector2(viewSize.X * UL.X, viewSize.Y * UL.Y);
        v2ScreenLR = new Vector2(viewSize.X * LR.X, viewSize.Y * LR.Y);
    }


    public void ToScreenPosition(in Vector4 v4View, out Vector2 v2Screen)
    {
        v2Screen.X = v4View.X / v4View.W;
        v2Screen.Y = v4View.Y / v4View.W;

        Vector2 glUL = UL;
        Vector2 glLR = LR;
        //glUL.Y *= -1;
        //glLR.Y *= -1;
        glUL *= 2f;
        glUL -= Vector2.One;
        glLR *= 2f; 
        glLR -= Vector2.One;
        Vector2 v2Size = glLR - glUL;
        v2Screen.X *= v2Size.X / 2f;
        v2Screen.Y *= v2Size.Y / 2f;
        //v2Screen -= (UL * 2f - Vector2.One) - (-Vector2.One);
        v2Screen -= UL * 2f;
    }
    
    
    public void GetViewMatrix(out Matrix4x4 matView, in Matrix4x4 mCameraToWorld)
    {
        var vCameraPosition = mCameraToWorld.Translation;
        Vector3 vY;
        Vector3 vUp = vY = new Vector3(mCameraToWorld.M21, mCameraToWorld.M22, mCameraToWorld.M23);
        Vector3 vZ = new Vector3(-mCameraToWorld.M31, -mCameraToWorld.M32, -mCameraToWorld.M33);
        Vector3 vFront = -vZ;
        Vector3 vTarget = vCameraPosition + vFront;
        Matrix4x4 mScaleToViewWindow = Matrix4x4.CreateScale(new Vector3(Scale, Scale, 1f));
        matView = Matrix4x4.CreateLookAt(vCameraPosition, vCameraPosition + vZ, vUp) * mScaleToViewWindow;
    }


    public void GetProjectionMatrix(out Matrix4x4 matProjection, in Vector2 vViewSize)
    {
        float realAngle = Angle;
        if (0f == Angle)
        {
            realAngle = 60f;
        }

        float right = NearFrustum * (float)Math.Tan(realAngle * 0.5f * (float)Math.PI / 180f);
        float invAspect = vViewSize.Y / vViewSize.X;
        float top = right * invAspect;

        float n = NearFrustum;
        float f = FarFrustum;
        float l = -right;
        float r = right;
        float t = top;
        float b = -top;

        if (Angle > 0f)
        {
            /*
             * This is the projection matrix for the usual order of matrix
             * multiplication. result = m * v;
             * ... however, this is not c# .net core convention.
             */
            Matrix4x4 m = new(
                2f * n / (r - l), 0f, (r + l) / (r - l), 0f,
                0f, 2f * n / (t - b), (t + b) / (t - b), 0f,
                0f, 0f, -(f + n) / (f - n), -2f * f * n / (f - n),
                0f, 0f, -1f, 0f
            );

            // TXWTODO: We need a smarter way to fix that to the view.
            Matrix4x4 mScaleToViewWindow = Matrix4x4.Identity;

            /*
             * Transpose this matrix to have it in c# convention, i.e.
             * result = v * m.
             */
            matProjection = Matrix4x4.Transpose(m);
        }
        else if (Angle == 0f)
        {
            /*
             * Orthogonal projection.
             */
            /* This is the projection matrix for the usual order of matrix
             * multiplication. result = m * v;
             * ... however, this is not c# .net core convention.
             */
            Matrix4x4 m = new(
                2f / (r - l), 0f, 0f, -(r + l) / (r - l),
                0f, 2f / (t - b), 0f, -(t + b) / (t - b),
                0f, 0f, -2f / (f - n), -(f + n) / (f - n),
                0f, 0f, 0f, 1f
            );

            // TXWTODO: We need a smarter way to fix that to the view.
            Matrix4x4 mScaleToViewWindow = Matrix4x4.CreateScale(new Vector3(Scale, Scale, 1f));

            /*
             * Transpose this matrix to have it in c# convention, i.e.
             * result = v * m.
             */
            matProjection = Matrix4x4.Transpose(m);
        }
        else
        {
            matProjection = Matrix4x4.Identity;

        }
    }

    public Camera3()
    {
    }
}
    
