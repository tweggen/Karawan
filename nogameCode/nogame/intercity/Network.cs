using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using engine.world;


namespace nogame.intercity;


public class LineDescription
{
    public ClusterDesc ClusterA;
    public ClusterDesc ClusterB;

    public string Hash()
    {
        string idA = ClusterA.IdString;
        string idB = ClusterB.IdString;
        if (string.CompareOrdinal(idA, idB) < 0)
        {
            return $"{idA}-{idB}";
        }
        else
        {
            return $"{idB}-{idA}";
        }
    }

    public LineDescription(ClusterDesc a, ClusterDesc b)
    {
        ClusterA = a;
        ClusterB = b;
    }
}


public class Line
{
    public ClusterDesc ClusterA;
    public ClusterDesc ClusterB;

    public Station StationA;
    public Station StationB;
    
    public float Width = 5f;
    public float Height = 0f;

    public string ToString()
    {
        return $"{ClusterA.IdString}-{ClusterB.IdString}";
    }
}


public class Station
{
    public Vector3 Position;
    public Vector2 Pos2
    {
        get => new Vector2(Position.X, Position.Z);
    }
    public string Name;

    public string Hash()
    {
        return Position.ToString();
    }
}


public class ClusterStations
{
    public ClusterDesc Cluster;
    public List<Station> Stations = new();
}


public class Network
{
    private object _lo = new();
    private SortedDictionary<string, Station> _mapStations = null;
    private SortedDictionary<string, Line> _mapLines = null;
    private Dictionary<ClusterDesc, ClusterStations> _mapClusterStations = null;
    
    private Station _findStation(Vector3 stationPos, ClusterDesc clusterDesc, string postfix)
    {
        Station station;
        if (_mapStations.TryGetValue(stationPos.ToString(), out station))
        {
            return station;
        }

        station = new();
        station.Position = stationPos;
        station.Name = $"{clusterDesc.Name} {postfix}";
        _mapStations.Add(stationPos.ToString(), station);

        if (!_mapClusterStations.TryGetValue(clusterDesc, out var clusterStations))
        {
            clusterStations = new() { Cluster = clusterDesc };
        }
        clusterStations.Stations.Add(station);
        
        return station;
    }


    private Vector3 _getStationPosition_nolock(ClusterDesc clusterA, ClusterDesc clusterB, out string postfix)
    {
        Vector3 ofs;
        Vector3 d = clusterB.Pos - clusterA.Pos;

        
        if (d.X > d.Z)
        {
            if (-d.X > d.Z)
            {
                ofs = new Vector3(0f, 0f, -1f);
                postfix = "south";
            }
            else
            {
                ofs = new Vector3(1f, 0f, 0f);
                postfix = "east";
            }
        }
        else
        {
            if (-d.X > d.Z)
            {
                ofs = new Vector3(-1f, 0f, 0f);
                postfix = "west";
            }
            else
            {
                ofs = new Vector3(0f, 0f, 1f);
                postfix = "north";
            }
        }

        return clusterA.Size / 2f * ofs + clusterA.Pos;
    }
    

    private Line _findLine_nolock(ClusterDesc clusterA, ClusterDesc clusterB)
    {
        LineDescription ld = new(clusterA, clusterB);
        if (_mapLines.TryGetValue(ld.Hash(), out var oldline))
        {
            return oldline;
        }
        
        Vector3 stationAPos = _getStationPosition_nolock(clusterA, clusterB, out var postfixA);
        Vector3 stationBPos = _getStationPosition_nolock(clusterB, clusterA, out var postfixB);

        var listTouchedClusters = ClusterList.Instance().IntersectsCluster(stationAPos, stationBPos);
        if (listTouchedClusters != null) 
        {
            /*
             * This line intersects other clusters, do not add it.
             */
            foreach (var c in listTouchedClusters)
            {
                if (c != clusterA && c != clusterB)
                {
                    return null;
                }
            }
        }
        
        Station stationA = _findStation(stationAPos, clusterA, postfixA);
        Station stationB = _findStation(stationBPos, clusterB, postfixB);
        
        Line line = new()
        {
            ClusterA = clusterA,
            ClusterB = clusterB,
            StationA = stationA,
            StationB = stationB,
            Height = Single.Min(clusterA.AverageHeight, clusterB.AverageHeight)
        };

        _mapLines.Add(ld.Hash(), line);
        return line;
    }
    
    
    private void _createNetwork()
    {
        lock (_lo)
        {
            if (_mapStations != null)
            {
                return;
            }

            _mapLines = new();
            _mapStations = new();
            _mapClusterStations = new();

            var clusterList = ClusterList.Instance().GetClusterList();
            foreach (ClusterDesc clusterDesc in clusterList)
            {
                int maxNTrams = 5;
                //    Int32.Clamp(0, 2999, (int)clusterDesc.Size - 800)
                //    / (3000/5) + 1;


                var closestClusters = clusterDesc.GetClosest();
                int ncc = closestClusters.Length;
                maxNTrams = Int32.Min(ncc, maxNTrams);

                int nTrams = 0;
                for (int i = 0; i < ncc; ++i)
                {
                    ClusterDesc other = closestClusters[i];
                    if (other == null) continue;

                    Line line = _findLine_nolock(clusterDesc, other);
                    if (line != null)
                    {
                        nTrams++;
                        if (maxNTrams == nTrams)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
    
    
    public IEnumerable<Line> Lines
    {
        get
        {
            _createNetwork();
            return _mapLines.Values;
        }
    }
}