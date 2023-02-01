using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets
{
    public class Quarter
    {

        private List<QuarterDelim> _delims;
        private bool _isInvalid = false;
        private bool _hasDeadEnd = false;
        private Vector2 _centerPoint;

        private List<Estate> _estates;
        private Dictionary<string, string> _debugMap;

        public void AddQuarterDelim(in QuarterDelim quarterDelim)
        {
            _delims.Add(quarterDelim);
        }

        public List<QuarterDelim> GetDelims()
        {
            return _delims;
        }

        public Vector2 GetCenterPoint()
        {
            return _centerPoint;
        }

        public void SetInvalid(bool i) 
        {
            _isInvalid = i;
        }

        public bool IsInvalid()
        {
            return _isInvalid;
        }

        public void SetDeadEnd(bool i) 
        {
            _hasDeadEnd = i;
        }

        public bool GetDeadEnd()
        {
            return _hasDeadEnd;
        }

        public List<Estate> GetEstates()
        {
            return _estates;
        }

        public void AddEstate(in Estate estate)
        {
            _estates.Add(estate);
        }

        public void ForDelims(Action<QuarterDelim, StreetPoint, StreetPoint> action)
        {
            QuarterDelim? lastDelim = null;
            foreach(var delim in _delims)
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
            int nPoints = 0;
            float cx = 0.0f;
            float cy = 0.0f;
            foreach(var delim in _delims)
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

        public void AddDebugTag(string key, string value )
        {
            _debugMap[key] = value;
        }

        public string GetDebugString()
        {
            string s = "{\n";
            foreach(KeyValuePair<string, string> kvp in _debugMap)
            {
                var value = kvp.Value;
                s += $"'{kvp.Key}': '{kvp.Value}',\n";
            }
            s += "}\n";
            return s;
        }


        public Quarter()
        {
            _delims = new List<QuarterDelim>();
            _estates = new List<Estate>();
            _debugMap = new Dictionary<string, string>();
            }
        }
}
