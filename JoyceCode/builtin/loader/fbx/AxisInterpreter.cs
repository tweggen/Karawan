using System;
using System.Numerics;

namespace builtin.loader.fbx;

public class AxisInterpreter
{
    private readonly Metadata _metadata;

    private Vector3 _v3Up;
    private Vector3 _v3Front;
    private Vector3 _v3Right;

    private bool _isLeftHanded = false;

    public Matrix4x4 M4ToJoyce;
    public Matrix4x4 M4FromJoyce;


    public Vector3 ToJoyce(in Vector3 v3) => Vector3.Transform(v3, M4ToJoyce);
    
    public Quaternion ToJoyce(in Quaternion q)
    {
        // q = [x, y, z, w] where (x,y,z) are the vector components
        Vector3 oldVec = new(q.X, q.Y, q.Z);
        
        // Apply the transformation to the quaternion's vector part
        float newX = M4ToJoyce.M11 * oldVec.X + M4ToJoyce.M12 * oldVec.Y + M4ToJoyce.M13 * oldVec.Z;
        float newY = M4ToJoyce.M21 * oldVec.X + M4ToJoyce.M22 * oldVec.Y + M4ToJoyce.M23 * oldVec.Z;
        float newZ = M4ToJoyce.M31 * oldVec.X + M4ToJoyce.M32 * oldVec.Y + M4ToJoyce.M33 * oldVec.Z;
        
        return new Quaternion(newX, newY, newZ, q.W); // w component unchanged
    }
    
    public unsafe AxisInterpreter(Metadata metadata)
    {
        _metadata = metadata;

        int upAxis = _metadata.GetInteger("UpAxis");
        int whichFrontAxis = _metadata.GetInteger("FrontAxis");
        
        int upSign = _metadata.GetInteger("UpAxisSign") == 1?1:-1;
        int frontSign = _metadata.GetInteger("FrontAxisSign") == 1?1:-1;
        int rightSign = _metadata.GetInteger("CoordAxisSign") == 1?1:-1;
        
        switch (upAxis)
        {
            case 1:
                _v3Up = Vector3.UnitX;
                if (whichFrontAxis == 1)
                {
                    _v3Front = Vector3.UnitY;
                    _v3Right = Vector3.UnitZ;
                }
                else
                {
                    _v3Front = Vector3.UnitZ;
                    _v3Right = Vector3.UnitY;
                }
                break;
            case 2:
            default:
                _v3Up = Vector3.UnitY;
                if (whichFrontAxis == 1)
                {
                    _v3Front = Vector3.UnitX;
                    _v3Right = Vector3.UnitZ;
                }
                else
                {
                    _v3Front = Vector3.UnitZ;
                    _v3Right = Vector3.UnitX;
                }
                break;
            case 3:
                _v3Up = Vector3.UnitZ;
                if (whichFrontAxis == 1)
                {
                    _v3Front = Vector3.UnitY;
                    _v3Right = Vector3.UnitZ;
                }
                else
                {
                    _v3Front = Vector3.UnitZ;
                    _v3Right = Vector3.UnitY;
                }
                break;
        }

        _v3Up *= upSign;
        _v3Front *= frontSign;
        _v3Right *= rightSign;

        _isLeftHanded = Vector3.Dot(Vector3.Cross(_v3Right, _v3Up), _v3Front) < 0;

        M4ToJoyce = new Matrix4x4(
            _v3Right.X, _v3Right.Y, _v3Right.Z, 0f,
            _v3Up.X, _v3Up.Y, _v3Up.Z, 0f,
            _v3Front.X, _v3Front.Y, _v3Front.Z, 0f,
            0f, 0f, 0f, 1f
        );
        Matrix4x4.Invert(M4ToJoyce, out M4FromJoyce);
    }
}