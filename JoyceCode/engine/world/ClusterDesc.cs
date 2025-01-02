using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using engine.geom;
using engine.joyce;
using engine.streets;
using static engine.Logger;

namespace engine.world;


public class ClusterDescConverter : JsonConverter<ClusterDesc>
{
    public override ClusterDesc Read(
        ref Utf8JsonReader reader,
        System.Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        int clusterId = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var clusterDesc = I.Get<ClusterList>().GetCluster(clusterId);
                return clusterDesc;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString();
                reader.Read();

                /*
                 * Note that we do not read all the properties but instead read the
                 * street point from the store.
                 */
                switch (propertyName)
                {
                    case nameof(ClusterDesc.Id):
                        clusterId = reader.GetInt32();
                        break;
                }
            }
        }

        throw new JsonException();
    }


    public override void Write(
        Utf8JsonWriter writer,
        ClusterDesc clusterDesc,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(ClusterDesc.Id), clusterDesc.Id);
        writer.WriteEndObject();
    }
}


/**
 * Describe a cluster.
 *
 * Note that all clusters will be serialized out to cache.
 */
public class ClusterDesc
{
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
    private float _initialOuterStreetWeight = 1.2f;
    private float _initialInnerStreetLength;
    private float _initialInnerStreetWeight = 1.1f;

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
     * Using the information about the next cities, create seed points for 
     * the map based on the interconnecting stations.
     */
    private void _addHighwayTriggers(Generator streetGenerator)
    {
        _initialOuterStreetLength =
            Single.Max(45f, Single.Min(1000f, Size) / 12f);
        _initialInnerStreetLength =
            Single.Max(45f, Single.Min(1000f, Size) / 16f);
        
        /*
         * Variant two: n random points
         */
        var nSeeds = (_rnd.Get8()>>5)+1;
        for( int i=0; i<nSeeds; ++i ) {
            engine.streets.StreetPoint newA = new engine.streets.StreetPoint() { ClusterId = this.Id };
            float x = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
            float y = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
            newA.SetPos( x, y );
            float dir = _rnd.Get8()*(float)Math.PI/128f;
            var newB = new engine.streets.StreetPoint() { ClusterId = this.Id };
            var stroke = engine.streets.Stroke.CreateByAngleFrom( this, 
                newA, newB, dir, 
                _initialInnerStreetLength, true, _initialInnerStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
#if true
        /*
         * Plus the four corners.
         */
        {
            var newA = new StreetPoint() { ClusterId = this.Id };
            newA.SetPos( -Size/2.2f, -Size/2.2f );
            var newB = new StreetPoint() { ClusterId = this.Id };
            var stroke = Stroke.CreateByAngleFrom( this,
                newA, newB, (float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = this.Id };
            newA.SetPos( Size/2.1f, -Size/2.1f );
            var newB = new StreetPoint() { ClusterId = this.Id };
            var stroke = Stroke.CreateByAngleFrom( this,
                newA, newB, 3f*(float)Math.PI*0.25f,
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = this.Id };
            newA.SetPos( -Size/2.2f, Size/2.2f );
            var newB = new StreetPoint() { ClusterId = this.Id };
            var stroke = Stroke.CreateByAngleFrom( this, 
                newA, newB, -(float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = this.Id };
            newA.SetPos( Size/2.15f, Size/2.2f );
            var newB = new StreetPoint() { ClusterId = this.Id };
            var stroke = Stroke.CreateByAngleFrom( this,
                newA, newB, -3.0f*(float)Math.PI*0.25f, 
                _initialOuterStreetLength, true, _initialOuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
#endif
    }


    private void _generateStrokes()
    {
        Generator streetGenerator = new Generator();
        streetGenerator.SetAnnotation($"Cluster {Name}");
        streetGenerator.Reset("streets-" + _strKey, _strokeStore, this);
        float terrainFacetSize =
            world.MetaGen.FragmentSize / (float) world.MetaGen.GroundResolution;
        streetGenerator.SetBounds(
            -Size / 2f + terrainFacetSize, 
            -Size / 2f + terrainFacetSize,
            Size / 2f - terrainFacetSize,
            Size / 2f - terrainFacetSize);
        _addHighwayTriggers(streetGenerator);
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
                Trace($"Loaded streets for {this.Name} from cache.");
            }
            else
            {
                Trace($"Generating streets for {this.Name}.");
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
        Trace($"Cluster {Name} triggering street generation #{_nStreetsTriggered}");
        /*
         * First, generate the actual streets.
         */
        _strokeStore = new streets.StrokeStore(Size);
        _quarterStore = new streets.QuarterStore(this);

        _findStrokes();

        _processStrokes();
        _findQuarters();

        Trace(
            $"Cluster {Name} has {_strokeStore.GetStreetPoints().Count} street points, {_strokeStore.GetStrokes().Count} street segments. Now calling cluster operators..."
        );

        _clusterState = ClusterState.Generating;
        
        I.Get<MetaGen>().ApplyClusterOperators(this);

        _clusterState = ClusterState.Done;
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
                /*
                 * 
                 */
                break;
            
            case LocationAttributes.Living:
                break;

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
