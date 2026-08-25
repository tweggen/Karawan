using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.streets.generation;


/**
 * The only place that is allowed to change the topology of a StrokeStore.
 *
 * Before this existed, Generator.Generate() reached into the store and rewired the
 * endpoints of an already-stored stroke inline. That is the operation with the most
 * invariants riding on it - two octrees, the adjacency set, the InStore flags and the
 * per-point angle arrays all have to stay in sync - and it was open coded in the
 * middle of a 1200 line method.
 *
 * WARNING: the order of operations in SplitStrokeAt is part of the generated output.
 * Points enter the point octree in insertion order, and octree order decides which
 * candidate wins a distance tie later on. Reordering the two AddStroke calls changes
 * street layouts. See docs/roadmap/proposed/STREETS-GENERATOR-REWORK-PLAN.md section 0.2.
 */
internal sealed class NetworkBuilder
{
    private readonly StrokeStore _strokeStore;


    internal NetworkBuilder(StrokeStore strokeStore)
    {
        _strokeStore = strokeStore ?? throw new ArgumentNullException(nameof(strokeStore));
    }


    /**
     * Add one stroke, enforcing the rules that keep the layer model coherent.
     *
     * A stroke whose endpoints sit on different decks is a ramp, and nothing else. If
     * an ordinary street were ever allowed to join two levels, it would render as a
     * road climbing through the air, and every level-filtered query would start
     * lying about what is reachable from where.
     */
    internal void Commit(Stroke stroke)
    {
        _checkLevels(stroke);
        _strokeStore.AddStroke(stroke);
    }


    /**
     * Add a whole structure, or none of it.
     *
     * A ramp - deck - ramp chain is only meaningful complete. Half a bridge is worse
     * than no bridge: it is a road that stops in mid air, and it would be indexed,
     * pathfound over and rendered like anything else. Every member is checked before
     * any member is added.
     */
    internal void CommitChain(IReadOnlyList<Stroke> chain)
    {
        if (null == chain || chain.Count == 0)
        {
            ErrorThrow("Cannot commit an empty chain.", m => new InvalidOperationException(m));
        }

        foreach (var stroke in chain)
        {
            _checkLevels(stroke);

            if (null != stroke.Store)
            {
                ErrorThrow($"Chain member {stroke} already is in a store.",
                    m => new InvalidOperationException(m));
            }
        }

        /*
         * Only now, once every member is known to be admissible.
         */
        foreach (var stroke in chain)
        {
            _strokeStore.AddStroke(stroke);
        }
    }


    private void _checkLevels(Stroke stroke)
    {
        sbyte levelA = stroke.A.Level;
        sbyte levelB = stroke.B.Level;

        if (levelA == levelB)
        {
            if (stroke.Kind == StrokeKind.Ramp)
            {
                ErrorThrow(
                    $"Ramp {stroke} joins two junctions on level {levelA}. A ramp exists " +
                    $"to change level; one that does not is an ordinary street.",
                    m => new InvalidOperationException(m));
            }

            return;
        }

        if (stroke.Kind != StrokeKind.Ramp)
        {
            ErrorThrow(
                $"Stroke {stroke} of kind {stroke.Kind} joins level {levelA} to level " +
                $"{levelB}. Only a ramp may change level.",
                m => new InvalidOperationException(m));
        }

        if (Math.Abs(levelA - levelB) != 1)
        {
            ErrorThrow(
                $"Ramp {stroke} spans from level {levelA} to level {levelB}. Ramps join " +
                $"adjacent decks only; further apart needs a ramp per level.",
                m => new InvalidOperationException(m));
        }
    }


    /**
     * Split an already-stored stroke in two at the given point.
     *
     * The stroke is removed from the store first: Stroke._setA/_setB refuse to
     * exchange an endpoint while the stroke is in a graph, which is exactly the
     * invariant that keeps a half-rewired stroke from ever reaching the octree.
     *
     * On return, `existing` runs from its original A to `at`, and the returned stroke
     * runs from `at` to the original B. Both are in the store, as is `at`.
     *
     * @param existing
     *     The stored stroke to split. Mutated in place to become the first half.
     * @param at
     *     The new junction. Must not be in the store yet.
     * @returns
     *     The newly created second half, already added to the store.
     */
    internal Stroke SplitStrokeAt(Stroke existing, StreetPoint at)
    {
        if (null == existing)
        {
            ErrorThrow($"Cannot split a null stroke.", m => new InvalidOperationException(m));
        }

        if (existing.Store != _strokeStore)
        {
            ErrorThrow($"Cannot split stroke {existing}: it is not in this store.",
                m => new InvalidOperationException(m));
        }

        if (at.InStore)
        {
            ErrorThrow($"Cannot split at point {at}: it already is in the store.",
                m => new InvalidOperationException(m));
        }

        /*
         * We must not modify the topology of the graph directly. Remove the edge
         * first, then modify the nodes, then re-add.
         */
        _strokeStore.Remove(existing);

        /*
         * Copied before `existing` is rewired, so it still carries the original
         * A and B.
         */
        Stroke tail = existing.CreateUnattachedCopy();
        tail.PushCreator("newStrokeExists");

        existing.B = at;
        existing.PushCreator("oldStrokeExists");

        tail.A = at;

        /*
         * Order matters, see the warning on this class.
         */
        _strokeStore.AddStroke(tail);
        _strokeStore.AddStroke(existing);

        return tail;
    }
}
