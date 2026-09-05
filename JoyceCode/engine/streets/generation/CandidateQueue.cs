using System.Collections.Generic;

namespace engine.streets.generation;


/**
 * The generator's queue of candidate strokes waiting to be judged.
 *
 * WHY THIS IS ITS OWN CLASS. Grade separation needs a structure to be placed on a
 * heavy corridor BEFORE side streets attach to it - otherwise a lift orphans whatever
 * has already grown off the corridor's interior. The plan's first realisation of that
 * was a second generation stage with its own rule table and a re-seeding walk over the
 * stage-one strokes; decision D2 threw all of it away in favour of the one thing that
 * gives the same ordering for free. Drain the queue heaviest first and a heavy corridor
 * is finished before any of its branches is popped, because a branch is emitted from an
 * already accepted stroke and is drawn from a weight group whose decrease probability is
 * 190 of 256.
 *
 * FLAG OFF THIS IS TODAY'S STACK, and deliberately the same List being popped from its
 * end rather than a lookalike: HeavyFirst false makes Pop() `RemoveAt(Count - 1)` and
 * nothing else, so the eight recorded fingerprints are what say the ordering did not
 * leak into the default city.
 *
 * The heavy-first scan is linear in the pending count. That is a real cost and it is
 * paid only with the flag on; a heap would buy it back but would also need its own tie
 * break, and a tie break is exactly where determinism gets lost. The list is scanned
 * BACKWARDS so that among equal weights the most recently pushed candidate wins, i.e.
 * within one weight the queue still behaves like the stack - which is what keeps the two
 * halves of a split in the order the split pushed them.
 */
internal sealed class CandidateQueue
{
    /**
     * Pending candidates in push order. Flag off, this is the whole data structure.
     */
    private readonly List<Stroke> _pending = new();


    /**
     * Whether to pop the heaviest pending candidate instead of the most recent one.
     *
     * Set once per run, from Generator.EnableGradeSeparation, when the pipeline is
     * built. Deliberately not a constructor argument: seeds are pushed before the flag
     * is necessarily known, and re-filing them later is a second ordering decision
     * nobody would remember to make.
     */
    internal bool HeavyFirst { get; set; }


    internal int Count => _pending.Count;


    internal void Push(in Stroke stroke)
    {
        _pending.Add(stroke);
    }


    internal Stroke Pop()
    {
        int idx = _pending.Count - 1;

        if (HeavyFirst)
        {
            float best = _pending[idx].Weight;

            for (int i = idx - 1; i >= 0; --i)
            {
                if (_pending[i].Weight > best)
                {
                    best = _pending[i].Weight;
                    idx = i;
                }
            }
        }

        Stroke stroke = _pending[idx];
        _pending.RemoveAt(idx);

        return stroke;
    }


    /**
     * Everything still waiting, in push order.
     *
     * Exists for the gates. B2.2 is a property of the order candidates actually leave
     * this queue in over a generated city, and a test that asked the comparer instead
     * would pass with the queue unwired - which is how WP-B1 found two constraints that
     * had had passing tests for months while sitting outside the pipeline.
     */
    internal IReadOnlyList<Stroke> Pending => _pending;
}
