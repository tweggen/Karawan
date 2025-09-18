using System;
using System.Numerics;
using static engine.Logger;

namespace builtin.loader.fbx;

public class AxisInterpreter
{
    private readonly Metadata _metadata;

    private Vector3 _v3Up;
    private Vector3 _v3Viewer;
    private Vector3 _v3Right;

    private bool _isLeftHanded = false;
    public bool IsLeftHanded => _isLeftHanded;
    
    public Matrix4x4 M4ToJoyce;
    public Matrix4x4 M4FromJoyce;

    public Matrix4x4 M4ToScaleJoyce;

    public Vector3 ToJoyce(in Vector3 v3) => Vector3.Transform(v3, M4ToJoyce);

    public Vector3 ToJoyceNormal(in Vector3 v3) => Vector3.Transform(v3, M4ToJoyce);  
        
        #if false
_isLeftHanded
        ? Vector3.Transform(v3, M4ToJoyce) * -1f 
        : Vector3.Transform(v3, M4ToJoyce) * 1f;
        #endif

    public Matrix4x4 ToJoyce(in Matrix4x4 m4)
    {
        if (Matrix4x4.Decompose(m4, out var v3Scale, out var qRotation, out var v3Translation))
        {
            return
                Matrix4x4.CreateScale(Vector3.Transform(v3Scale, M4ToScaleJoyce))
                *
                Matrix4x4.CreateFromQuaternion(ToJoyce(qRotation))
                *
                Matrix4x4.CreateTranslation(Vector3.Transform(v3Translation, M4ToJoyce));
        }
        else
        {
            /*
             * Not decomposable? Then do a brute-force transformation.
             */
            return M4FromJoyce * m4 * M4ToJoyce;
        }
    }

    public Vector3 ToJoyceScale(in Vector3 v3) => Vector3.Transform(v3, M4ToScaleJoyce);

    public Quaternion ToJoyceHandedness(in Quaternion q)
    {
        if (!_isLeftHanded)
        {
            return q;
        }
        else
        {
            return new Quaternion(-q.X, -q.Y, -q.Z, q.W);
        }
    } 
    
    
    public Quaternion ToJoyce(in Quaternion q)
    {
#if false
        return q;
#else
        // q = [x, y, z, w] where (x,y,z) are the vector components
        Vector3 oldVec = new(q.X, q.Y, q.Z);
        
        // Apply the transformation to the quaternion's vector part
        float newX = M4ToJoyce.M11 * oldVec.X + M4ToJoyce.M12 * oldVec.Y + M4ToJoyce.M13 * oldVec.Z;
        float newY = M4ToJoyce.M21 * oldVec.X + M4ToJoyce.M22 * oldVec.Y + M4ToJoyce.M23 * oldVec.Z;
        float newZ = M4ToJoyce.M31 * oldVec.X + M4ToJoyce.M32 * oldVec.Y + M4ToJoyce.M33 * oldVec.Z;

        if (!_isLeftHanded)
        {
            return new Quaternion(newX, newY, newZ, q.W); // w component unchanged
        }
        else
        {
            return new Quaternion(-newX, -newY, -newZ, q.W); // w component unchanged
        }
#endif
    }


    private void _afterInit()
    {
        _isLeftHanded = Vector3.Dot(Vector3.Cross(_v3Right, _v3Up), _v3Viewer) < 0;

        M4ToJoyce = new Matrix4x4(
            _v3Right.X, _v3Right.Y, _v3Right.Z, 0f,
            _v3Up.X, _v3Up.Y, _v3Up.Z, 0f,
            _v3Viewer.X, _v3Viewer.Y, _v3Viewer.Z, 0f,
            0f, 0f, 0f, 1f
        );

        M4ToScaleJoyce = new Matrix4x4(
            Single.Abs(_v3Right.X), Single.Abs(_v3Right.Y), Single.Abs(_v3Right.Z), 0f,
            Single.Abs(_v3Up.X), Single.Abs(_v3Up.Y), Single.Abs(_v3Up.Z), 0f,
            Single.Abs(_v3Viewer.X), Single.Abs(_v3Viewer.Y), Single.Abs(_v3Viewer.Z), 0f,
            0f, 0f, 0f, 1f
        );
        Matrix4x4.Invert(M4ToJoyce, out M4FromJoyce);
    }

    public AxisInterpreter(in Vector3 v3Right, in Vector3 v3Up, in Vector3 v3Viewer)
    {
        _v3Viewer = v3Viewer;
        _v3Up = v3Up;
        _v3Right = v3Right;

        _afterInit();
    }
    
    
    public unsafe AxisInterpreter(Metadata metadata)
    {
        _metadata = metadata;

        int upSign = _metadata.GetInteger("UpAxisSign") == 1?1:-1;
        int viewerSign = _metadata.GetInteger("FrontAxisSign") == 1?-1:1;
        int rightSign = _metadata.GetInteger("CoordAxisSign") == 1?1:-1;

        Vector3[] arrAxis = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

        _v3Viewer = arrAxis[_metadata.GetInteger("FrontAxis", 2)];
        _v3Up = arrAxis[_metadata.GetInteger("UpAxis", 1)];
        _v3Right = arrAxis[_metadata.GetInteger("CoordAxis", 0)];
        
        _v3Up *= upSign;
        _v3Viewer *= viewerSign;
        _v3Right *= rightSign;

        _afterInit();
    }


    public static AxisInterpreter CreateFromString(string desc)
    {
        if (desc.Length < 3)
        {
            ErrorThrow<ArgumentException>($"Invalid axis description: {desc}");
        }
        
        Vector3[] v3AxisSystem = new Vector3[3];
        
        for (int i = 0; i < 3; i++)
        {
            Vector3 v3Axis;
            switch (desc[i] & 0x5f)
            {
                case 'X':
                    v3Axis = Vector3.UnitX;
                    break;
                case 'Y':
                    v3Axis = Vector3.UnitY;
                    break;
                case 'Z':
                    v3Axis = Vector3.UnitZ;
                    break;
                default:
                    v3Axis = Vector3.UnitX;
                    break;
            }

            if ((desc[i] & 0x20) == 0x20)
            {
                v3Axis = -v3Axis;
            }
            v3AxisSystem[i] = v3Axis;
        }
        
        return new AxisInterpreter(v3AxisSystem[0], v3AxisSystem[1], v3AxisSystem[2]);
    }
}