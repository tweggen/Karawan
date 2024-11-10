#if false
using engine.streets;
using static builtin.Workarounds;
using static engine.Logger;

namespace nogame.cities;

public class RandomPathEnumerator
{
    private StreetPoint _findNextStroke(StreetPoint spFrom, StreetPoint spBy)
    {
        StreetPoint? spTo = null;
        
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

            _currentStroke = strokes[idx];
            if (null == _currentStroke)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): strokes[{idx}] is null.");
            }

            if (null == _currentStroke.A)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): _currentStroke.A is null.");
            }

            if (null == _currentStroke.B)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): _currentStroke.B is null.");
            }

            if (_currentStroke.A == spBy)
            {
                //_isAB = true;
                spTo = _currentStroke.B;
            }
            else
            {
                //isAB = false;
                spTo = _currentStroke.A;
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
            if (1 == targetStrokes.Count && _avoidDeadEnds)
            {

                if (nTries < strokes.Count)
                {
                    spTo = null;
                    continue;
                }
            }

            if (spTo != spFrom)
            {
                break;
            }

        }

        return spTo;
    }
}
#endif