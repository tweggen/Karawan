using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using engine.geom;
using engine.joyce;
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
    private static readonly engine.Dc _dc = engine.Dc.MetaGen;

    [Flags]
    public enum LocationAttributes {
        Downtown = 0x00000001,
        Shopping = 0x00000002,
        Living = 0x00000004,
        Industrial = 0x00000008
    };
    
    
    [LiteDB.BsonId]
    public int Id { get; set; } = 0;
    
    /**
     * To protect me, especially generating streets
     */
    private object _lo = new();

    private enum ClusterState
    {
        Created,
        Triggered,
        Computing,
        Generating,
        Done
    };

    private ClusterState _clusterState = ClusterState.Created;
    public bool IsCompleted => _clusterState == ClusterState.Done;

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

    /**
     * Where the ground is under this city's junctions.
     *
     * Everything that emits street geometry asks this rather than reading
     * AverageHeight directly, so that the whole city moves onto the terrain by
     * swapping one object. Created lazily because the flat source needs
     * AverageHeight, which ClusterBaseElevationOperator computes some time after
     * this descriptor exists.
     */
    private streets.IStreetHeightSource _streetHeightSource;


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
    

    private Dictionary<Index3, ImmutableList<StreetPoint>> _mapFragmentPoints = null;

    /**
     * Return this cluster's streetpoints that are inside the fragment.
     */
    public IImmutableList<StreetPoint> GetStreetPointsInFragment(Index3 idxFragment)
    {
        lock (_lo)
        {
            /*
             * If we do not know the street points per fragment, compute it now.
             */
            if (_mapFragmentPoints == null)
            {
                _triggerStreets_nl();
                _mapFragmentPoints = new();

                int _si = (int)(world.MetaGen.MaxWidth / world.MetaGen.FragmentSize);
                int _sk = (int)(world.MetaGen.MaxHeight / world.MetaGen.FragmentSize);


                AABB aabbCluster = AABB;
                Index3 fragMin = new(
                    int.Clamp((int)(aabbCluster.AA.X / world.MetaGen.FragmentSize), -_si, _si),
                    0,
                    int.Clamp((int)(aabbCluster.AA.Z / world.MetaGen.FragmentSize), -_sk, _sk)
                );
                Index3 fragMax = new(
                    int.Clamp((int)((aabbCluster.BB.X + world.MetaGen.FragmentSize - 1f) / world.MetaGen.FragmentSize),
                        -_si, _si),
                    0,
                    int.Clamp((int)((aabbCluster.BB.Z + world.MetaGen.FragmentSize - 1f) / world.MetaGen.FragmentSize),
                        -_sk, _sk)
                );

                Dictionary<Index3, List<StreetPoint>> mapListSP = new();

                foreach (var sp in _strokeStore.GetStreetPoints())
                {
                    Index3 idxSP = Fragment.PosToIndex3(sp.Pos3 + Pos);
                    List<StreetPoint> spList;
                    if (!mapListSP.TryGetValue(idxSP, out spList))
                    {
                        spList = new List<StreetPoint>();
                        mapListSP[idxSP] = spList;
                    }

                    spList.Add(sp);
                }

                foreach (var kvp in mapListSP)
                {
                    _mapFragmentPoints[kvp.Key] = kvp.Value.ToImmutableList();
                }
            }

            /*
             * After we computed it, either return a list of fragments or an empty list.
             */
            if (_mapFragmentPoints.TryGetValue(idxFragment, out var listPoints))
            {
                return listPoints;
            }
            else
            {
                return ImmutableList<StreetPoint>.Empty;
            }
        }
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
     * Expose the cluster's random source to the street seeding code (and to the
     * deterministic street test harness), so both draw from the very same generator
     * state that the game uses.
     */
    internal builtin.tools.RandomSource Rnd
    {
        get => _rnd;
    }

    /**
     * Read the street generation ruleset from the Mix configuration.
     *
     * A missing section is fine and yields the built-in defaults, which are
     * value-identical to it. A malformed one is not: it is reported and the defaults
     * are used, because a half-applied ruleset would quietly reshape every city.
     */
    private streets.generation.ExpansionRuleTable _loadStreetGenRuleTable()
    {
        try
        {
            return streets.generation.StreetGenConfig.Parse(
                I.Get<engine.casette.Mix>().GetTree("/streetGen"));
        }
        catch (System.Exception e)
        {
            Error($"Invalid street generation ruleset, falling back to defaults: {e.Message}");
            return streets.generation.ExpansionRuleTable.Defaults();
        }
    }


    private void _generateStrokes()
    {
        Generator streetGenerator = new Generator();
        streetGenerator.SetAnnotation($"Cluster {Name}");
        streetGenerator.Reset("streets-" + _strKey, _strokeStore, this);
        streetGenerator.RuleTable = _loadStreetGenRuleTable();
        streets.StreetSeeds.ApplyBounds(streetGenerator, this);
        streets.StreetSeeds.AddTo(streetGenerator, this, _rnd);
        streetGenerator.Generate();
        streetGenerator = null;
    }


    private void _findStrokes()
    {
        using (new builtin.tools.SectionMeter("ClusterDesc._findStrokes"))
        {
            bool haveStoredStreets = I.Get<ClusterStorage>().TryLoadClusterStreets(this);
            if (haveStoredStreets)
            {
                /*
                 * Nothing to do here yet.
                 */
                Trace(_dc, $"Loaded streets for {this.Name} from cache.");
            }
            else
            {
                Trace(_dc, $"Generating streets for {this.Name}.");
                _generateStrokes();
                I.Get<ClusterStorage>().StoreClusterStreetPoints(this);
                I.Get<ClusterStorage>().StoreClusterStrokes(this);
            }
        }
    }
    

    private void _processStrokes()
    {
        _strokeStore.PolishStreetPoints();
        
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
        _quarterGenerator = new streets.QuarterGenerator();
        _quarterGenerator.Reset("quarters-" + _strKey, this, _quarterStore, _strokeStore);
        _quarterGenerator.Generate();
    }


    private int _nStreetsTriggered = 0;
    
    private void _triggerStreets_nl()
    {
        if (_clusterState >= ClusterState.Triggered)
        {
            return;
        }

        _clusterState = ClusterState.Computing;
        
        _nStreetsTriggered++;
        Trace(_dc, $"Cluster {Name} triggering street generation #{_nStreetsTriggered}");
        /*
         * First, generate the actual streets.
         */
        _strokeStore = new streets.StrokeStore(Size);
        _quarterStore = new streets.QuarterStore(this);

        _findStrokes();

        _processStrokes();
        _findQuarters();

        Trace(_dc,
            $"Cluster {Name} has {_strokeStore.GetStreetPoints().Count} street points, {_strokeStore.GetStrokes().Count} street segments. Now calling cluster operators..."
        );

        _clusterState = ClusterState.Generating;
        
        I.Get<MetaGen>().ApplyClusterOperators(this);

        _clusterState = ClusterState.Done;

        Trace(_dc, $"TALE DIAG: Pushing ClusterCompletedEvent for cluster '{Name}' (index {Index}).");
        I.Get<engine.news.EventQueue>().Push(new ClusterCompletedEvent(Name));
    }


    /**
     * Load or compute the streets of this city.
     */
    private void _triggerStreets()
    {
        lock (_lo)
        {
            _triggerStreets_nl();
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


    /**
     * Where the ground is under this city's junctions.
     *
     * Flat unless joyce.DisableClusterFlattening says the terrain has been left
     * alone, in which case the junctions sample it. The two decisions are one
     * setting on purpose: sampling terrain that has just been ironed flat would
     * merely be a slower way of reading the average, and following terrain that is
     * still being flattened elsewhere is the inconsistent half state.
     */
    public streets.IStreetHeightSource StreetHeightSource
    {
        get
        {
            /*
             * Double checked, because this is read from hot paths - WalkController asks
             * for a walking height every frame - and taking the cluster's lock there
             * would contend with street and quarter generation for a field that is
             * written once and never changes.
             */
            var existing = System.Threading.Volatile.Read(ref _streetHeightSource);
            if (null != existing)
            {
                return existing;
            }

            lock (_lo)
            {
                if (null == _streetHeightSource)
                {
                    System.Threading.Volatile.Write(
                        ref _streetHeightSource, streets.StreetHeightSources.For(this));
                }

                return _streetHeightSource;
            }
        }

        /**
         * For tests, which describe a slope as a function rather than as terrain.
         */
        set
        {
            lock (_lo)
            {
                System.Threading.Volatile.Write(ref _streetHeightSource, value);
            }
        }
    }


    /**
     * Height of the ground at an arbitrary point in this city, in world space.
     *
     * The companion to IStreetHeightSource.GroundHeightAt, for the many callers that
     * have a position rather than a junction - a vehicle in motion, a spawn point, a
     * route being built. Anything that puts a moving thing in the world goes through
     * here, so that "where is the ground" is answered in ONE place and a city cannot
     * end up with its streets on the terrain and its traffic at the average.
     *
     * A flat city answers from the average, which is exact: the terrain really has been
     * ironed flat to it. Otherwise it samples the terrain.
     *
     * Deliberately the TERRAIN and not the street surface, even in the middle of a road.
     * Those are different quantities - streets are relaxed to buildable gradients, so
     * they cut into hills and stand proud of dips - and they converge only once a
     * corridor-conforming pass rewrites the ground along the roads. Until then this is
     * right off the road and out by the cut or fill on it, which is a small and even
     * error rather than the whole relief of the city.
     */
    public float GroundHeightAt(in Vector3 v3World)
    {
        if (StreetHeightSource.IsFlat)
        {
            return AverageHeight;
        }

        return I.Get<MetaGen>().Loader.GetHeightAt(v3World.X, v3World.Z);
    }


    public void FindStartPosition(out Vector3 v3Start, out Quaternion qStart)
    {
        var vOffset = new Vector3(0f, 0f, -3f);
        v3Start = new();
        qStart = new();
        
        /*
         * Cluster relative coordinates of the start.
         */
        Vector3 v3ClusterStart = new();
        
        _triggerStreets();
        bool havePos = false;
        foreach (var quarter in _quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            foreach (var estate in quarter.GetEstates())
            {
                if (estate.GetBuildings().Count == 0)
                {
                    v3ClusterStart = (estate.GetCenter() + vOffset) with { Y = AverageHeight + 100f };
                    v3Start = v3ClusterStart with { Y = AverageHeight + 100f };
                    havePos = true;
                    break;
                }
            }

            if (havePos) break;
        }

        if (havePos)
        {
            /*
             * We do have a start position, let's face the center of the city.
             */
            Vector3 vuZ = v3ClusterStart with { Y = 0f };
            try
            {
                vuZ = Vector3.Normalize(vuZ);
                qStart = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateWorld(Vector3.Zero, -vuZ, Vector3.UnitY));
            }
            catch (Exception e)
            {
                qStart = Quaternion.Identity;
            }
        }

        if (!havePos)
        {
            /*
             * If we didn't find anything, position ourselves in the center of the cluster, facing north.
             */
            v3Start = (Pos + vOffset) with { Y = AverageHeight + 100f };
            qStart = Quaternion.Identity;
        }
        
        if (!(Single.IsFinite(qStart.X) && Single.IsFinite(qStart.Y) && Single.IsFinite(qStart.Z) && Single.IsFinite(qStart.W)))
        {
            Debug.Assert(false, "Invalid quaternion!");
            qStart = Quaternion.Identity;
        }
    }


    /**
     * Return the intensity of that location with respect to the given attribute.
     */
    public float GetAttributeIntensity(Vector3 v3Spot, LocationAttributes locAttr)
    {
        // TXWTODO: This all is hard-coded right now.
        switch (locAttr)
        {
            case LocationAttributes.Downtown:
            {
                /*
                 * Downtown is quite in the middle of the city.
                 */
                var dist = (_pos - v3Spot).Length();
                dist = dist / (_size / 2f);
                var gauss = Single.Exp(-(dist * dist));
                return gauss;
            }

            case LocationAttributes.Industrial:
            {
                // Outermost area - inverse of downtown
                // High intensity at periphery, low towards center
                var dist = (_pos - v3Spot).Length();
                dist = dist / (_size / 2f);
                // Invert: high when far from center
                var intensity = 1.0f - Single.Exp(-(dist * dist) * 2f);
                return intensity;
            }

            case LocationAttributes.Living:
            {
                // Ring around downtown/shopping, overlapping with shopping ring
                // Residential neighborhoods form a middle band with commerce nearby
                var dist = (_pos - v3Spot).Length();
                dist = dist / (_size / 2.5f) + 0.3f;  // Wider radius than shopping
                var gauss = Single.Exp(-(dist * dist));
                return gauss;
            }

            case LocationAttributes.Shopping:
            {
                var dist = (_pos - v3Spot).Length();
                /*
                 * Leave out the very center and the outskirts from shopping
                 */
                dist = dist / (_size / 3f) + 0.2f;
                var gauss = Single.Exp(-(dist * dist));
                return gauss;
            }
        }

        /*
         * Return the equivalent of I don't know.
         */
        return 0.5f;
    }
}
