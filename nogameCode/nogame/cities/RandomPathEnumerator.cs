using System;
using System.Collections;
using System.Collections.Generic;
using builtin.tools;
using engine.streets;
using static builtin.Workarounds;
using static engine.Logger;

namespace nogame.cities;

public class RandomPathEnumerator : System.Collections.Generic.IEnumerator<(Stroke,StreetPoint)>
{
    public bool AvoidDeadEnds { init; get; } = false;


    private RandomSource _rnd;

    private Stroke _startStroke;
    private StreetPoint _startStreetPoint;

    private StreetPoint _previousStreetPoint = null;
    
    private Stroke _currentStartStroke;
    private StreetPoint _currentStreetPoint;
    
    private (Stroke,StreetPoint) _findNextStroke(StreetPoint spFrom, StreetPoint spBy)
    {
        StreetPoint? spTo = null;
        Stroke? strokeTo = null;
        
        /*
         * We select a random stroke and use its destination point.
         * If the destination point has one stroke only (which must be
         * the one we origin from), we consider that street point only
         * if dead ends are allowed.
         */
        IList<Stroke> strokes = spBy.GetAngleArray();
        if (0 == strokes.Count)
        {
            ErrorThrow("Encountered empty street point.", m => new InvalidOperationException(m));
        }

        var nTries = 0;

        while (true)
        {
            ++nTries;
            var idx = (int)(_rnd.GetFloat() * strokes.Count);

            strokeTo = strokes[idx];
            if (null == strokeTo)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): strokes[{idx}] is null.");
            }

            if (null == strokeTo.A)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): strokeTo.A is null.");
            }

            if (null == strokeTo.B)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): strokeTo.B is null.");
            }

            if (strokeTo.A == spBy)
            {
                //_isAB = true;
                spTo = strokeTo.B;
            }
            else
            {
                //isAB = false;
                spTo = strokeTo.A;
            }

            /*
             * Is the target point the prevoius point?
             * That's a valid path of course.
             */
            var targetStrokes = spTo.GetAngleArray();

            /*
             * There's no other path? Then take it.
             */
            if (strokes.Count == 1)
            {
                break;
            }

            /*
             * OK, target point is not the previous point amd not the only option.
             */
            if (1 == targetStrokes.Count && AvoidDeadEnds)
            {

                if (nTries < strokes.Count)
                {
                    spTo = null;
                    strokeTo = null;
                    continue;
                }
            }

            if (spTo != spFrom)
            {
                break;
            }

        }

        return (strokeTo, spTo);
    }


    public bool MoveNext()
    {
        StreetPoint currentStreetPoint = _currentStreetPoint; 
        (_currentStartStroke, _currentStreetPoint) = _findNextStroke(_previousStreetPoint, currentStreetPoint);
        _previousStreetPoint = currentStreetPoint;
        return true;
    }

    public void Reset()
    {
        _previousStreetPoint = null;
        _currentStartStroke = _startStroke;
        _currentStreetPoint = _startStreetPoint;
    }

    public (Stroke, StreetPoint) Current
    {
        get => (_currentStartStroke, _currentStreetPoint);
    }

    object IEnumerator.Current => Current;

    
    public void Dispose()
    {
        /*
         * We do not have anything to dispose.
         */
        return;
    }
    
    
    public RandomPathEnumerator(RandomSource rnd, Stroke? startStroke, StreetPoint spStart)
    {
        _rnd = rnd;
        _startStroke = startStroke;
        _startStreetPoint = spStart;

        // This is doubled reset code.
        _previousStreetPoint = null;
        _currentStartStroke = _startStroke;
        _currentStreetPoint = _startStreetPoint;
    }
}
