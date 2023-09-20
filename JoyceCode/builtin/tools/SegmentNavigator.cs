using System;
using System.Numerics;
using engine;

namespace Joyce.builtin.tools;

public class SegmentEnd
{
    public Vector3 Position;
    public Vector3 Up;
    public Vector3 Right;
}

/**
 * Implement a navigator for a run between two points.
 * Can take several objects that shall be navigated along that way.
 *
 * Segment navigator may be used over large distances. To avoid a continuous
 * iteration over the behavior, we will need to be able to derive the position/state
 * from the "absolute" time. 
 */
public class SegmentNavigator : INavigator
{
    private float _speed = 50f;
    private bool _isReturning = false;

    private float _relativePos = 0f;
    private float _absolutePos = 0f;
    private float _distance = 0f;

    private Vector3 _vPosition;
    private Quaternion _qOrientation;
    
    private SegmentEnd _a;
    public SegmentEnd A
    {
        get => _a;
    }
    private SegmentEnd _b;

    public SegmentEnd B
    {
        get => _b;
    }
    
    
    public float Speed
    {
        get => _speed;
        set => _speed = value;
    }


    public bool IsReturning
    {
        get => _isReturning;
        set => _isReturning = value;
    }


    public void NavigatorGetTransformation(out System.Numerics.Vector3 position, out Quaternion orientation)
    {
        position = _vPosition;
        orientation = _qOrientation;
    }

    
    public void NavigatorSetTransformation(System.Numerics.Vector3 vPos3, Quaternion qOrientation)
    {
        // Not supported.
    }

    
    public void NavigatorBehave(float dt)
    {
        float totalTogo = dt * _speed;

        Vector3 vuForward = Vector3.One;
        while (totalTogo > 0.001)
        {
            float togo;
            float direction;

            if (_isReturning)
            {
                togo = _absolutePos;
                direction = -1f;
                vuForward = (_a.Position - _b.Position) / _distance;
            }
            else
            {
                togo = _distance - _absolutePos;
                direction = 1f;
                vuForward = (_b.Position - _a.Position) / _distance;
            }

            togo = Single.Min(totalTogo, togo);
            _absolutePos += direction * togo;
            totalTogo -= togo;
            if (Single.Abs(_absolutePos - _distance) < 0.001)
            {
                _isReturning = !_isReturning;
            }
        }

        _relativePos = _absolutePos / _distance;
        _vPosition = _a.Position + (_b.Position - _a.Position) * _relativePos 
                                   + _a.Right * (_isReturning?-0.5f:0.5f);
        _qOrientation = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateWorld(
                Vector3.Zero, 
                vuForward, 
                new Vector3(0f, 1f, 0f)));
    }
    
    
    public SegmentNavigator(SegmentEnd a, SegmentEnd b)
    {
        _a = a;
        _b = b;
        _distance = (b.Position - a.Position).Length();
    }
}