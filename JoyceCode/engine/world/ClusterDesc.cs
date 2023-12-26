using System;
using System.Numerics;
using engine.streets;
using static engine.Logger;

namespace engine.world;


/**
 * Describe a cluster.
 *
 * Note that all clusters will be serialized out to cache.
 */
public class ClusterDesc
{
    [LiteDB.BsonId]
    public int Id { get; set; } = 0;
    
    /**
     * To protect me, especially generating streets
     */
    private object _lo = new();

    public string IdString
    {
        get => _strKey;
        set
        {
            _strKey = value;
            _rnd = new builtin.tools.RandomSource(_strKey);
        }
    }

    public bool Merged = false;

    private Vector3 _pos;
    public Vector3 Pos
    {
        get => _pos;
        set => _setPos(value);
    }

    [LiteDB.BsonIgnore]
    public Vector2 Pos2
    {
        get => new Vector2(Pos.X, Pos.Z);
    }

    private engine.geom.Rect2 _rect2;
    [LiteDB.BsonIgnore]
    public engine.geom.Rect2 Rect2
    {
        get => _rect2;
    }
    
    private float _size = 100;
    public float Size
    {
        get => _size;
        set => _setSize(value);
    }

    public int Index { get; set; } = -1;
    
    public string Name { get; set; } = "Unnamed";

    
    /*
     * This will not be serialized out, we generate that automatically.
     */
    public float AverageHeight = 0f;

    private const int _maxClosest = 5;

    private ClusterDesc[] _arrCloseCities = new ClusterDesc[_maxClosest];
    private int _nClosest = 0;
    private string _strKey;
    private builtin.tools.RandomSource _rnd;
    private engine.geom.AABB _aabb;

    private float _initialOuterStreetLength;
    private float _initialOuterStreetWeight = 1.7f;
    private float _initialInnerStreetLength;
    private float _initialInnerStreetWeight = 1.4f;

    /** 
     * Each cluster has a stroke store associated that descirbes the 
     * street graph.
     */
    private streets.StrokeStore _strokeStore;

    /**
     * In addition, each cluster has a lot generator associated.
     */
    private streets.QuarterGenerator _quarterGenerator;

    /**
     * This is the store for all quarters we generated.
     */
    private streets.QuarterStore _quarterStore;


    private void _setSize(float size)
    {
        _size = size;
        _aabb = new geom.AABB(_pos, size);
        _rect2 = new()
        {
            A = new(_pos.X - _size / 2f, _pos.Z - _size / 2f),
            B = new(_pos.X + _size / 2f, _pos.Z + _size / 2f),
        };
    }
    
    private void _setPos(Vector3 pos)
    {
        _pos = pos;
        _aabb = new geom.AABB(pos, _size);
        _rect2 = new()
        {
            A = new(_pos.X - _size / 2f, _pos.Z - _size / 2f),
            B = new(_pos.X + _size / 2f, _pos.Z + _size / 2f),
        };
    }
    
    public override string ToString()
    {
        return $"{{ 'id': {_strKey}; 'name': {Name}; 'pos': {Pos}; 'size': {Size}; }}";
    }

    public string GetKey()
    {
        return _strKey;
    }


    public bool IsInside(in Vector3 p)
    {
        if (
            p.X >= (Pos.X - Size / 2f) && p.X <= (Pos.X + Size / 2f)
            && p.Z >= (Pos.Z - Size / 2f) && p.Z <= (Pos.Z + Size / 2f)
        )
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    [LiteDB.BsonIgnore]
    public engine.geom.AABB AABB
    {
        get => _aabb; 
    }
    public void GetAABB(out engine.geom.AABB aabb)
    {
        aabb = _aabb;
    }
    

    public streets.Quarter GuessQuarter(in Vector2 p)
    {
        _triggerStreets();
        /*
         * Fast wrong implementation: Find the closest center point.
         */
        var pc = p;
        pc.X -= Pos.X;
        pc.Y -= Pos.Y;
        float minDist = 9999999999f;
        streets.Quarter minQuarter = null;
        foreach(var quarter in _quarterStore.GetQuarters() )
        {
            var cp = quarter.GetCenterPoint();
            float dist = Vector2.Distance( cp, pc );
            if (dist < minDist)
            {
                minDist = dist;
                minQuarter = quarter;
            }
        }
        return minQuarter;
    }


    public ClusterDesc[] GetClosest()
    {
        return _arrCloseCities;
    }


    public int GetNClosest()
    {
        return _nClosest;
    }


    public void AddClosest(in ClusterDesc other)
    {
        lock(_lo)
        { 
            if (other == this) return;

            float distance = (float)Vector3.Distance(other.Pos, this.Pos);

            // Special case first.
            if (0 == _nClosest)
            {
                _arrCloseCities[0] = other;
                _nClosest = 1;
                return;
            }

            // Now insert, whereever required
            int idx = 0;
            while (idx < _nClosest)
            {
                ClusterDesc cl = _arrCloseCities[idx];
                // Also ignore this if already known.
                if (cl == other)
                {
                    idx++;
                    return;
                }

                float clDist = (float)Vector3.Distance(cl.Pos, this.Pos);

                if (distance < clDist)
                {
                    // Smaller distance? Then insert myself here.
                    int idx2 = idx + 1;
                    int max = _nClosest + 1;
                    if (max > _maxClosest) max = _maxClosest;
                    while (idx2 < max)
                    {
                        _arrCloseCities[idx2] = _arrCloseCities[idx2 - 1];
                        ++idx2;
                    }
                    _arrCloseCities[idx] = other;
                    _nClosest = max;
                    // Inserted.
                    return;
                }
                idx++;
            }
        }
    }


    /**
     * Using the information about the next cities, create seed points for 
     * the map based on the interconnecting stations.
     */
    private void _addHighwayTriggers(Generator streetGenerator)
    {
        _initialOuterStreetLength =
            Single.Max(35f, Single.Min(1000f, Size) / 18f);
        _initialInnerStreetLength =
            Single.Max(30f, Single.Min(1000f, Size) / 25f);
        
        /*
         * Variant two: n random points
         */
        var nSeeds = (_rnd.Get8()>>5)+1;
        for( int i=0; i<nSeeds; ++i ) {
            engine.streets.StreetPoint newA = new engine.streets.StreetPoint();
            float x = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
            float y = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
            newA.SetPos( x, y );
            float dir = _rnd.Get8()*(float)Math.PI/128f;
            var newB = new engine.streets.StreetPoint();
            var stroke = engine.streets.Stroke.CreateByAngleFrom( newA, newB, dir, 
                _initialInnerStreetLength, true, _initialInnerStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
#if true
        /*
         * Plus the four corners.
         */
        {
            var newA = new StreetPoint();
            newA.SetPos( -Size/2.2f, -Size/2.2f );
            var newB = new StreetPoint();
            var stroke = Stroke.CreateByAngleFrom( newA, newB, (float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint();
            newA.SetPos( Size/2.1f, -Size/2.1f );
            var newB = new StreetPoint();
            var stroke = Stroke.CreateByAngleFrom( newA, newB, 3f*(float)Math.PI*0.25f,
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint();
            newA.SetPos( -Size/2.2f, Size/2.2f );
            var newB = new StreetPoint();
            var stroke = Stroke.CreateByAngleFrom( newA, newB, -(float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint();
            newA.SetPos( Size/2.15f, Size/2.2f );
            var newB = new StreetPoint();
            var stroke = Stroke.CreateByAngleFrom( newA, newB, -3.0f*(float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
#endif
    }


    private void _findStrokes()
    {
        /*
         * First, generate the actual streets.
         */
        _strokeStore = new streets.StrokeStore(Size);

        Generator streetGenerator = new Generator();
        streetGenerator.SetAnnotation($"Cluster {Name}");
        streetGenerator.Reset("streets-" + _strKey, _strokeStore, this);
        streetGenerator.SetBounds(-Size / 2f, -Size / 2f, Size / 2f, Size / 2f);
        _addHighwayTriggers(streetGenerator);
        streetGenerator.Generate();
        streetGenerator = null;
    }


    private void _processStrokes()
    {
        /*
         * Unfortunately, we also need to generate the sections at this point.
         */
        foreach (var sp in _strokeStore.GetStreetPoints())
        {
            sp.GetSectionArray();
        }

    }


    private void _findQuarters()
    {
        /*
         * Now compute the quarters from the streets.
         */
        _quarterStore = new streets.QuarterStore();
        _quarterGenerator = new streets.QuarterGenerator();
        _quarterGenerator.Reset("quarters-" + _strKey, _quarterStore, _strokeStore);
        _quarterGenerator.Generate();
    }
    

    /**
     * Load or compute the streets of this city.
     */
    private void _triggerStreets()
    {
        lock (_lo)
        {
            if (null == _strokeStore)
            {
                _findStrokes();
                _processStrokes();
                _findQuarters();
                
                Trace(
                    $"Cluster {Name} has {_strokeStore.GetStreetPoints().Count} street points, {_strokeStore.GetStrokes().Count} street segments."
                    );
            }
        }
    }


    public streets.StrokeStore StrokeStore() 
    {
        _triggerStreets();
        return _strokeStore;
    }


    public streets.QuarterStore QuarterStore() 
    {
        _triggerStreets();
        return _quarterStore;
    }


    public Vector3 FindStartPosition()
    {
        var vOffset = new Vector3(0f, 0f, -3f);
        
        _triggerStreets();
        foreach (var quarter in _quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            foreach (var estate in quarter.GetEstates())
            {
                if (estate.GetBuildings().Count == 0)
                {
                    return (Pos + estate.GetCenter() + vOffset) with { Y = AverageHeight + 3f };
                }
            }
        }
        return (Pos+vOffset) with { Y = AverageHeight + 3f };
    }
}
