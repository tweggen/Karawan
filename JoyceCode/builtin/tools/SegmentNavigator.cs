using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave;
using builtin.extensions;
using static engine.Logger;

namespace builtin.tools;


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
    private object _lo = new();
    
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
    
    private readonly List<SegmentEnd> _listSegments;
    private readonly SegmentRoute _segmentRoute;
    public SegmentRoute SegmentRoute
    {
        get => _segmentRoute;
        init
        {
            if (value.Segments.Count < 2)
            {
                ErrorThrow("List of segments must contain at least 2 items.", m => new ArgumentException(m));
            }
            
            _segmentRoute = value;
            _listSegments = value.Segments;
        }
    }

    private PositionDescription _roPosition = null;
    private PositionDescription _position = null;

    /**
     * The current position of the navigator.
     * If not set up at startup time, the navigation starts at the native start of the route.
     */
    public PositionDescription? Position
    {
        get
        {
            lock (_lo)
            {
                if (null == _roPosition)
                {
                    if (null != _position)
                    {
                        _roPosition = new PositionDescription(_position);
                    }
                }

                return _roPosition;
            }
        }
        set
        {
            lock (_lo)
            {
                _position = new PositionDescription(value);
                _roPosition = null;
            }
        }
    }
    

    private float _speed = 50f;
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
        _a = null;
        _b = null;
    }


    private void _prepareSegment(float relativePosition = 0)
    {
        _vForward = (_b.Position - _a.Position);
        _distance = (_b.Position - _a.Position).Length();
        _vuForward = _vForward / _distance;
        _absolutePos = relativePosition * _distance;
    }



    private void _setABFromIndex(int idx, out SegmentEnd a, out SegmentEnd b, out int idxNextSegment)
    {
        int l = _listSegments.Count;
        a = _listSegments[(idx+0) % l];
        b = _listSegments[(idx+1) % l];
        idxNextSegment = (idx+2) % l;
    }


    private PositionDescription _defaultPosition()
    {
        int defaultStartIndex = 0;
        float defaultStartRelative = 0f;

        var seg0 = _listSegments[defaultStartIndex];

        if (seg0.PositionDescription != null)
        {
            return new PositionDescription(seg0.PositionDescription);
        }
        else
        {
            /*
             * Unfortunately, this lacks any semantic information.
             */
            return new PositionDescription()
            {
                Position = seg0.Position,
                Orientation = Quaternion.CreateFromRotationMatrix(
                    Matrix4x4Extensions.CreateFromUnitAxis(
                        seg0.Right, seg0.Up, Vector3.Cross(seg0.Right, seg0.Up)))
            };
        }
    }
    

    /**
     * Make sure we have a position record.
     * If we do not have any, create the default from the segment list.
     */
    private void _ensurePosition()
    {
        lock (_lo)
        {
            if (null == _position)
            {
                _position = _defaultPosition();
                _roPosition = null;
            }
        }
    }
    

    /**
     * Setup the initial segment. This is called once to initialize the route.
     * If no start position is known, it is loaded from Position.
     * If Position is not defined, it is reset to the native starting point. 
     */
    private void _setStartSegment()
    {
        _ensurePosition();
        _setABFromIndex(_position.QuarterDelimIndex, out _a, out _b, out _idxNextSegment);
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
            _prepareSegment(_position.RelativePos);
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

        float relativePos = _absolutePos / _distance;
        _vPosition = _a.Position + (_b.Position - _a.Position) * relativePos;
        _qOrientation = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateWorld(
                Vector3.Zero, 
                _vuForward, 
                new Vector3(0f, 1f, 0f)));
    }


    public void NavigatorLoad()
    {
        _resetTravel();
    }
}
