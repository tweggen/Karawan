using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.world;

namespace JoyceCode.Tests.engine.world;


/**
 * The shipped world's cities and the intercity lines between them, reached from a test.
 *
 * The cluster list is the REAL one: engine.world.GenerateClustersOperator's own
 * _generateClusterList, seeded exactly as MetaGen seeds it ("mydear"), so what this
 * measures cannot drift away from what the game lays out. It needs no container - the
 * generator only reads static MetaGen bounds and its own RandomSource - and its
 * MetaGen argument is unused.
 *
 * Two things ARE reproductions and are named as such:
 *
 *   - AverageHeightOf. ClusterBaseElevationOperator averages the elevation SEGMENT under
 *     the cluster rectangle, which needs the elevation cache and the stitcher; this
 *     averages the shipped terrain on the same 20 m grid over the same rectangle. It is
 *     used for distributions, never for an equality.
 *   - Lines. nogame.intercity.Network lives in nogameCode, which this assembly does not
 *     reference, so its line selection (each city's five closest, skipping any line that
 *     crosses a third city, deduplicated by an ordered id pair) is reproduced here. It
 *     runs against the real ClusterList and the real ClusterDesc.AddClosest.
 *
 * No guarantee in this folder rests on either reproduction. They exist so that "how far
 * does the intercity tram move" is a measured distribution over the world the game
 * actually builds rather than an adjective.
 */
internal static class IntercityWorldHarness
{
    private static readonly object _lo = new();
    private static List<ClusterDesc> _clusters;
    private static Dictionary<ClusterDesc, float> _aver;


    /**
     * The shipped world's cities, from the operator's own generator.
     */
    internal static List<ClusterDesc> Clusters()
    {
        lock (_lo)
        {
            if (null != _clusters) return _clusters;

            var op = new GenerateClustersOperator("mydear");
            op._generateClusterList(null, out var list);
            _clusters = new List<ClusterDesc>(list);

            _aver = new Dictionary<ClusterDesc, float>();
            foreach (var cd in _clusters)
            {
                cd.AverageHeight = _averageHeightOf(cd);
                _aver[cd] = cd.AverageHeight;
            }

            return _clusters;
        }
    }


    internal static float AverageHeightOf(ClusterDesc cd)
    {
        Clusters();
        return _aver[cd];
    }


    private static float _averageHeightOf(ClusterDesc cd)
    {
        var r = cd.Rect2;
        float step = MetaGen.FragmentSize / MetaGen.GroundResolution;
        int n = 0;
        double sum = 0;
        for (float z = r.A.Y; z < r.B.Y; z += step)
        {
            for (float x = r.A.X; x < r.B.X; x += step)
            {
                sum += JoyceCode.Tests.engine.streets.ShippedTerrain.HeightAt(x, z);
                ++n;
            }
        }

        return (float)(sum / n);
    }


    internal sealed class Line
    {
        internal ClusterDesc A, B;
        internal Vector3 PosA, PosB;

        /**
         * nogame.intercity.Network's own expression, through the shared function.
         */
        internal float TrackHeight => IntercityLine.TrackHeightOf(A.AverageHeight, B.AverageHeight);

        /**
         * What the vehicle's two route ends were before this was fixed.
         */
        internal float ShippedEndA => A.AverageHeight + 20f;
        internal float ShippedEndB => B.AverageHeight + 20f;
    }


    /**
     * nogame.intercity.Network._getStationPosition_nolock.
     */
    private static Vector3 _stationPos(ClusterDesc a, ClusterDesc b)
    {
        Vector3 ofs;
        Vector3 d = b.Pos - a.Pos;
        if (d.X > d.Z)
        {
            ofs = -d.X > d.Z ? new Vector3(0f, 0f, -1f) : new Vector3(1f, 0f, 0f);
        }
        else
        {
            ofs = -d.X > d.Z ? new Vector3(-1f, 0f, 0f) : new Vector3(0f, 0f, 1f);
        }

        return a.Size / 2f * ofs + a.Pos;
    }


    private static List<Line> _lines;


    /**
     * nogame.intercity.Network._createNetwork.
     */
    internal static List<Line> Lines()
    {
        lock (_lo)
        {
            if (null != _lines) return _lines;
        }

        var acd = Clusters();
        var clusterList = new ClusterList();
        clusterList.SetFrom(acd);
        for (int i = 0; i < acd.Count; ++i)
        {
            for (int j = 0; j < acd.Count; ++j)
            {
                acd[i].AddClosest(acd[j]);
            }
        }

        var mapLines = new SortedDictionary<string, Line>();
        foreach (var cd in acd)
        {
            var closest = cd.GetClosest();
            int maxN = Math.Min(closest.Length, 5);
            int nTrams = 0;
            foreach (var other in closest)
            {
                if (other == null) continue;

                string ia = cd.IdString, ib = other.IdString;
                string hash = string.CompareOrdinal(ia, ib) < 0 ? $"{ia}-{ib}" : $"{ib}-{ia}";
                if (!mapLines.TryGetValue(hash, out var line))
                {
                    var pa = _stationPos(cd, other);
                    var pb = _stationPos(other, cd);
                    var touched = clusterList.IntersectsCluster(pa, pb);
                    bool blocked = false;
                    if (touched != null)
                    {
                        foreach (var c in touched)
                        {
                            if (c != cd && c != other) { blocked = true; break; }
                        }
                    }

                    if (blocked) continue;

                    line = new Line { A = cd, B = other, PosA = pa, PosB = pb };
                    mapLines.Add(hash, line);
                }

                ++nTrams;
                if (maxN == nTrams) break;
            }
        }

        lock (_lo)
        {
            _lines ??= mapLines.Values.ToList();
            return _lines;
        }
    }
}
