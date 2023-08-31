using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets;

public class Quarter
{
    private object _lo = new();

    private List<QuarterDelim> _delims = new();
    private bool _isInvalid = false;
    private bool _hasDeadEnd = false;
    private Vector2 _centerPoint;

    private List<Estate> _estates = new();
    private Dictionary<string, string> _debugMap = new();

    public void AddQuarterDelim(in QuarterDelim quarterDelim)
    {
        lock (_lo)
        {
            _delims.Add(quarterDelim);
        }
    }

    public List<QuarterDelim> GetDelims()
    {
        lock (_lo)
        {
            return _delims;
        }
    }

    public Vector2 GetCenterPoint()
    {
        lock (_lo)
        {
            return _centerPoint;
        }
    }

    public void SetInvalid(bool i)
    {
        lock (_lo)
        {
            _isInvalid = i;
        }
    }

    public bool IsInvalid()
    {
        lock (_lo)
        {
            return _isInvalid;
        }
    }

    public void SetDeadEnd(bool i)
    {
        lock (_lo)
        {
            _hasDeadEnd = i;
        }
    }

    public bool GetDeadEnd()
    {
        lock (_lo)
        {
            return _hasDeadEnd;
        }
    }

    public List<Estate> GetEstates()
    {
        lock (_lo)
        {
            return _estates;
        }
    }

    public void AddEstate(in Estate estate)
    {
        lock (_lo)
        {
            _estates.Add(estate);
        }
    }

    public void ForDelims(Action<QuarterDelim, StreetPoint, StreetPoint> action)
    {
        #if false
        List<QuarterDelim> listDelims;

        lock (_lo)
        {
            listDelims = new(_delims);
        }
        #endif
        QuarterDelim? lastDelim = null;
        foreach (var delim in _delims)
        {
            if (lastDelim != null)
            {
                action(lastDelim, lastDelim.StreetPoint, delim.StreetPoint);
                lastDelim = delim;
            }
        }

        if (lastDelim != null)
        {
            var delim = _delims[0];
            action(lastDelim, lastDelim.StreetPoint, delim.StreetPoint);
        }
    }

    /**
     * Compute things like the quarter center.
     */
    public void Polish()
    {
        lock (_lo)
        {
            int nPoints = 0;
            float cx = 0.0f;
            float cy = 0.0f;
            foreach (var delim in _delims)
            {
                if (null != delim.StartPoint)
                {
                    ++nPoints;
                    cx += delim.StartPoint.X;
                    cy += delim.StartPoint.Y;
                }
            }

            if (nPoints > 0)
            {
                cx /= nPoints;
                cy /= nPoints;
                _centerPoint = new Vector2(cx, cy);
            }
            // else leave zero.
        }
    }

    public void AddDebugTag(string key, string value)
    {
        lock (_lo)
        {
            _debugMap[key] = value;
        }
    }

    public string GetDebugString()
    {
        lock (_lo)
        {
            string s = "{\n";
            foreach (KeyValuePair<string, string> kvp in _debugMap)
            {
                var value = kvp.Value;
                s += $"'{kvp.Key}': '{kvp.Value}',\n";
            }

            s += "}\n";
            return s;
        }
    }

}
