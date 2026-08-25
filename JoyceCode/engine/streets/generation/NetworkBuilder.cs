using System;
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
