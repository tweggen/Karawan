using System;
using System.Collections.Generic;
using engine.joyce;

namespace engine.tale;

/// <summary>
/// TALE S0 W3 (KAR-447): which fragment owns each materialised TALE NPC's
/// entity. An entity dies with the fragment that created it (its Owner), not
/// with the fragment its schedule position lies in, so despawn bookkeeping
/// must follow the owner: purging a fragment dematerialises exactly the NPCs
/// whose entity it owns.
///
/// An entry is written when the entity is set up (together with the spawn
/// status' InCreation--), so an NPC still in creation has no entry and is
/// counted once, by InCreation.
///
/// Not thread-safe: the caller (TaleSpawnOperator) guards it with its lock.
/// The liveness predicate is called from those methods only, which the
/// operator calls on the logical thread.
/// </summary>
public class TaleEntityOwnership<TEntity>
{
    private readonly Func<TEntity, bool> _isAlive;
    private readonly Dictionary<int, (uint OwnerKey, TEntity Entity)> _byNpc = new();


    public TaleEntityOwnership(Func<TEntity, bool> isAlive)
    {
        _isAlive = isAlive ?? throw new ArgumentNullException(nameof(isAlive));
    }


    public int Count => _byNpc.Count;


    /// <summary>
    /// The fragment that created this NPC's entity owns it from now on.
    /// </summary>
    public void Record(int npcId, Index3 ownerFragment, TEntity entity)
    {
        _byNpc[npcId] = (ownerFragment.AsKey(), entity);
    }


    public bool TryGet(int npcId, out TEntity entity)
    {
        if (_byNpc.TryGetValue(npcId, out var e))
        {
            entity = e.Entity;
            return true;
        }
        entity = default;
        return false;
    }


    /// <summary>
    /// Remove and return every NPC whose entity the given fragment owns:
    /// these entities die with the fragment.
    /// </summary>
    public List<(int NpcId, TEntity Entity)> RemoveOwnedBy(Index3 ownerFragment)
    {
        uint key = ownerFragment.AsKey();
        var result = new List<(int, TEntity)>();
        foreach (var (npcId, e) in _byNpc)
        {
            if (e.OwnerKey == key) result.Add((npcId, e.Entity));
        }
        foreach (var (npcId, _) in result) _byNpc.Remove(npcId);
        return result;
    }


    /// <summary>
    /// Remove and return the NPCs owned by this fragment whose entity is no
    /// longer alive although the fragment still is: the wiper killed it, or
    /// it died some other way.
    /// </summary>
    public List<int> RemoveDeadOwnedBy(Index3 ownerFragment)
    {
        uint key = ownerFragment.AsKey();
        var result = new List<int>();
        foreach (var (npcId, e) in _byNpc)
        {
            if (e.OwnerKey == key && !_isAlive(e.Entity)) result.Add(npcId);
        }
        foreach (var npcId in result) _byNpc.Remove(npcId);
        return result;
    }


    /// <summary>
    /// How many of these NPCs have a live entity, wherever it is owned: the
    /// spawn status' resident count for the fragment they are bucketed in.
    /// NPCs still in creation have no entry and are not counted.
    /// </summary>
    public int CountAlive(IEnumerable<int> npcIds)
    {
        int n = 0;
        foreach (var npcId in npcIds)
        {
            if (_byNpc.TryGetValue(npcId, out var e) && _isAlive(e.Entity)) n++;
        }
        return n;
    }
}
