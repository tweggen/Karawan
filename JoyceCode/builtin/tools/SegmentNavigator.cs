using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using engine;
using engine.behave;
using static engine.Logger;

namespace builtin.tools;

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

    private float _relativePos = 0f;
    private float _absolutePos = 0f;

    /*
     * The properties of the current segment as computed by
     * _prepareSegment
     */
    private float _distance = 0f;
    private Vector3 _vForward;
    private Vector3 _vuForward;

    private Vector3 _vPosition;
    private Quaternion _qOrientation;

    private SegmentEnd? _a = null;
    private SegmentEnd? _b = null;

    private int _idxNextSegment;
    private List<SegmentEnd> _listSegments;


    private bool _loopSegments;
    public bool LoopSegments
    {
        get => _loopSegments;
        set => _loopSegments = value;
    }
    
    
    public float Speed
    {
        get => _speed;
        set => _speed = value;
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


    private void _resetTravel()
    {
        _idxNextSegment = 0;
        _a = null;
        _b = null;
    }


    private void _prepareSegment()
    {
        _vForward = (_b.Position - _a.Position);
        _distance = (_b.Position - _a.Position).Length();
        _vuForward = _vForward / _distance;
        _absolutePos = 0;
    }
    

    /**
     * Set rup the initial segment.
     */
    private void _setStartSegment()
    {
        _a = _listSegments[0];
        _b = _listSegments[1];
        _idxNextSegment = 2 % _listSegments.Count;
    }


    private void _shiftForward()
    {
        _a = _b;
        _b = null;
    }


    private void _setNextSegment()
    {
        _b = _listSegments[_idxNextSegment];
        _idxNextSegment = (_idxNextSegment + 1) % _listSegments.Count;
    }
    
    
    public void NavigatorBehave(float dt)
    {
        float totalTogo = dt * _speed;

        if (null == _a)
        {
            _setStartSegment();
            _prepareSegment();
        }
            
        while (totalTogo > 0.001)
        {
            float togo;
            float direction;
            bool doTurnaround = false;

            togo = _distance - _absolutePos;
            direction = 1f;

            togo = Single.Min(totalTogo, togo);
            _absolutePos += direction * togo;
            totalTogo -= togo;

            if (Single.Abs(_absolutePos - _distance) < 0.001)
            {
                doTurnaround = true;
            }

            if (doTurnaround)
            {
                _shiftForward();
                _setNextSegment();
                _prepareSegment();
            }
        }

        _relativePos = _absolutePos / _distance;
        _vPosition = _a.Position + (_b.Position - _a.Position) * _relativePos 
                                   + _a.Right * 5.0f;
        _qOrientation = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateWorld(
                Vector3.Zero, 
                _vuForward, 
                new Vector3(0f, 1f, 0f)));
    }


    public Func<Task> SetupFrom(JsonElement je) => new(async () =>
    {
        //_qPrevRotation = ToQuaternion(jo["sno"]["prevRotation"]);
    });

    
    public void SaveTo(JsonObject jo)
    {
        JsonObject joNav = new JsonObject();
        joNav.Add("speed", _speed );
        jo.Add("nav", joNav);
    }
    

    public SegmentNavigator(List<SegmentEnd> listSegments)
    {
        if (listSegments.Count < 2)
        {
            ErrorThrow("List of segments must contain at least 2 items.", m => new ArgumentException(m));
        }
        _listSegments = listSegments;
        _resetTravel();
    }
}
