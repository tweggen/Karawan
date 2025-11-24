using System;
using System.Collections.Generic;
using System.Numerics;
using engine.geom;

namespace engine.streets;

public class Quarter
{
    private object _lo = new();

    public required engine.world.ClusterDesc ClusterDesc; 
    
    private List<QuarterDelim> _delims = new();
    private bool _isInvalid = false;
    private bool _hasDeadEnd = false;

    private List<Estate> _estates = new();
    private Dictionary<string, string> _debugMap = new();
    private AABB _aabb = new();

    public enum LocationAttributes
    {
        Forest = 0x00000002,
        Building = 0x00000004
    }
    
    public AABB AABB {
        get => _aabb;
        
    }

    /**
     * The delims are stored clockwise in the quarter.
     */
    public void AddQuarterDelim(in QuarterDelim quarterDelim)
    {
        lock (_lo)
        {
            _aabb.Add(new Vector3(quarterDelim.StartPoint.X, 0f, quarterDelim.StartPoint.Y));
            _delims.Add(quarterDelim);
        }
    }

    /**
     * The delims are stored clockwise in the quarter.
     */
    public List<QuarterDelim> GetDelims()
    {
        lock (_lo)
        {
            return _delims;
        }
    }

    public Vector2 GetCenterPoint()
    {
        return new Vector2(AABB.Center.X, AABB.Center.Z);
    }

    public Vector3 GetCenterPoint3()
    {
        return new Vector3(AABB.Center.X, 0f, AABB.Center.Z);
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


    /**
     * Compute things like the quarter center.
     */
    public void Polish()
    {
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
