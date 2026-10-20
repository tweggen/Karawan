using System;
using System.Collections.Generic;
using engine.joyce;

namespace engine.tale;

/// <summary>
/// TALE S0 W4 (KAR-434): which loaded fragments keep each cluster's NPCs
/// alive. There is no cluster-unload event, only fragment purges, so TALE
/// counts the resident fragments of every cluster itself and depopulates a
/// cluster once none has been resident for a grace period.
///
/// A fragment counts for every cluster whose area it intersects, not only for
/// the one the heat map names, so a border fragment keeps both neighbours.
/// The grace period damps flapping at a cluster edge and guarantees the
/// purged fragments' entities are gone before the schedules are dropped.
///
/// Written from the spawn operator (poll and purge) and read from the TALE
/// social frame, so it takes its own lock. Time is passed in, so tests need
/// no clock.
/// </summary>
public class TaleClusterResidency
{
    private readonly object _lo = new();
    private readonly Dictionary<int, HashSet<uint>> _residentByCluster = new();
    private readonly Dictionary<int, DateTime> _emptySince = new();


    /// <summary>
    /// The fragment is loaded and polled, and intersects this cluster: the
    /// cluster stays populated, and a pending depopulate is cancelled.
    /// </summary>
    public void FragmentActive(int clusterIndex, in Index3 idxFragment)
    {
        lock (_lo)
        {
            if (!_residentByCluster.TryGetValue(clusterIndex, out var set))
            {
                set = new HashSet<uint>();
                _residentByCluster[clusterIndex] = set;
            }
            set.Add(idxFragment.AsKey());
            _emptySince.Remove(clusterIndex);
        }
    }


    /// <summary>
    /// The fragment unloaded. Every cluster it was the last resident fragment
    /// of starts its grace period now.
    /// </summary>
    public void FragmentPurged(in Index3 idxFragment, DateTime now)
    {
        uint key = idxFragment.AsKey();
        lock (_lo)
        {
            foreach (var (clusterIndex, set) in _residentByCluster)
            {
                if (set.Remove(key) && set.Count == 0)
                {
                    _emptySince[clusterIndex] = now;
                }
            }
        }
    }


    public int ResidentCount(int clusterIndex)
    {
        lock (_lo)
        {
            return _residentByCluster.TryGetValue(clusterIndex, out var set) ? set.Count : 0;
        }
    }


    /// <summary>
    /// The populated clusters that have had no resident fragment for at least
    /// <paramref name="grace"/>. A populated cluster never seen resident (one
    /// populated on completion, far from the player) starts its grace period
    /// at the first call that sees it, so it is depopulated too.
    /// </summary>
    public List<int> DueForDepopulate(IEnumerable<int> populatedClusters, DateTime now, TimeSpan grace)
    {
        var due = new List<int>();
        lock (_lo)
        {
            foreach (int clusterIndex in populatedClusters)
            {
                if (_residentByCluster.TryGetValue(clusterIndex, out var set) && set.Count > 0) continue;
                if (!_emptySince.TryGetValue(clusterIndex, out var since))
                {
                    _emptySince[clusterIndex] = now;
                    continue;
                }
                if (now - since >= grace) due.Add(clusterIndex);
            }
        }
        return due;
    }


    /// <summary>
    /// The cluster was depopulated: forget its grace period. It starts a new
    /// one only after it is populated and left again.
    /// </summary>
    public void Depopulated(int clusterIndex)
    {
        lock (_lo)
        {
            _emptySince.Remove(clusterIndex);
        }
    }
}
