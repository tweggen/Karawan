using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static engine.Logger;

namespace engine.streets.generation;


/**
 * Why a corridor was not lifted.
 *
 * Named rather than counted, because "no bridges appeared" and "the policy never ran"
 * have to be different observations. Every refusal below is reached by real cities; the
 * counts per seed are in §11 of STREETS-3D-PHASE-B-CROSSING-POLICY.
 */
public enum StructureRefusal
{
    /**
     * The interior junction would be left with fewer than two arms - see
     * StructurePlacer's class comment.
     */
    InteriorTBranch,

    /**
     * One of the two through arms is too short to hold a ramp and still leave the deck
     * standing clear of the junction it flies over.
     */
    ArmTooShort,

    /**
     * OverpassBuilder could not describe a chain between these two feet at all.
     */
    NotAStructure,

    /**
     * The deck is shorter than MinSpanLength or longer than MaxSpanLength.
     */
    SpanLength,

    /**
     * The straight chain misses the road it was supposed to fly over. A corridor bends
     * at its crossing; the structure does not.
     */
    MissesWhatItCrosses,

    /**
     * A road comes within RampClearance of one of the ramps, or crosses it.
     */
    RampClearance,

    /**
     * The deck passes too close over the road beneath it.
     */
    DeckClearance,

    /**
     * A ramp did not come out at the grade it was designed to - i.e. the profile refused
     * it. A backstop: on a well formed chain both ramps are exact by construction.
     */
    RampGrade,

    /**
     * ⚠️ The one the plan did not have. The deck is steeper than the road it carries may
     * be - GradePolicy.MaxDeckGradeFor.
     */
    DeckGrade,

    /**
     * Another structure already claims one of these strokes or junctions.
     */
    OverlapsAStructure,

    /**
     * ⚠️ Lifting this corridor would leave part of the city unreachable.
     *
     * A grade separated crossing has no slip roads: the through road and the road
     * beneath it stop being connected to each other AT THAT POINT. Usually the city
     * closes over that, and on the shipped terrain it always does - but not always, and
     * a road nothing can reach is worse than an at-grade crossing.
     */
    WouldDisconnect
}


/**
 * What one cluster's placement pass did, in enough detail to argue with.
 */
public sealed class StructurePlacementReport
{
    /**
     * Junctions carrying a straight-through pair of ordinary streets - i.e. every
     * corridor the policy looked at.
     */
    public int Considered;

    public int Placed;

    public readonly Dictionary<StructureRefusal, int> Refused = new();

    /**
     * Deck grade of each structure that was built, as rise over run.
     */
    public readonly List<float> DeckGrades = new();

    /**
     * Vertical clearance between a deck and each road passing under it, in metres.
     */
    public readonly List<float> Clearances = new();


    public int RefusedTotal => Refused.Values.Sum();


    internal void Refuse(StructureRefusal reason)
    {
        Refused.TryGetValue(reason, out int n);
        Refused[reason] = n + 1;
    }


    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(
            $"{Considered} straight-through crossings considered, {Placed} structures placed");

        if (Placed > 0)
        {
            sb.Append(
                $" (deck grade {DeckGrades.Min():P1}..{DeckGrades.Max():P1}");
            if (Clearances.Count > 0)
            {
                sb.Append($", clearance {Clearances.Min():F2}..{Clearances.Max():F2} m");
            }
            sb.Append(')');
        }

        foreach (var kv in Refused.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
        {
            sb.Append($"; refused {kv.Key} x{kv.Value}");
        }

        return sb.ToString();
    }
}


/**
 * Puts grade separated structures on a finished street network.
 *
 * ## What a structure IS here
 *
 * One crossing, lifted. An interior junction `m` whose arms include a nearly opposed
 * pair - the road running straight through - has that pair REPLACED by a
 * ramp-deck-ramp chain between the two far ends, so the through road climbs, flies over
 * `m`, and comes back down. `m` itself stays exactly where it is and keeps its other
 * arms: the road that used to cross now passes underneath.
 *
 * The two lifted strokes are removed rather than kept. Keeping them would put a ground
 * road and a ramp along the same line, which is the one thing ClearanceConstraint exists
 * to forbid, and would leave the at-grade crossing the structure was built to remove.
 *
 * ## ⚠️ The interior-T-branch rule, and it is an EXCLUSION
 *
 * Take the through pair away from a junction with three arms and one arm is left: a road
 * that now ends nowhere, hanging off a junction that is no longer a junction. The plan
 * offered two answers - exclude such corridors, or re-attach the arm at a foot - and this
 * is the exclusion. Re-attaching moves the stub's junction by a ramp length, which moves
 * its blocks, its estate and its buildings, in service of a road that is not why the
 * structure is being built; and the thing that would then pass under the deck is a
 * dead-end stub, so there is nothing there worth flying over in the first place. A
 * crossing worth lifting is one where the road underneath goes somewhere, which is
 * exactly "the interior junction keeps at least two arms".
 *
 * ## Where the heights come from, and why this can refuse on them at all
 *
 * §3b of the plan says there is no relaxed height at generation time, and that is true of
 * RelaxedStreetHeight - asking it here would recurse through ClusterDesc.StrokeStore().
 * But the quantity a refusal needs is not the relaxed height of the finished city; it is
 * the ANCHOR height, and GradeRelaxer.Relax computes that by relaxing the network as if
 * the structures were not there. That is a pure function of (ground strokes, sampled
 * terrain, policy) and this pass can compute it directly - from the same unrelaxed source
 * the game will use, over exactly the ground strokes the finished network will have.
 *
 * So the grades refused on here are not an estimate of the game's: they ARE the game's,
 * by construction. The one thing that makes them so is that lifting a corridor removes
 * its two arms, which changes the ground network the anchor pass runs over - hence the
 * fixed-point loop below.
 */
public static class StructurePlacer
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;


    /**
     * How opposed the two arms of a corridor have to be to count as one road running
     * through. -0.9 is about 25 degrees off straight, and is the same threshold §2's
     * corridor-fit table was measured with, so the counts here are comparable to it.
     */
    public const float MinStraightDot = -0.9f;


    /**
     * Least vertical distance between a deck and the road under it.
     *
     * DERIVED, not chosen: MetaGen.ClusterNavigationHeight is the height the game's own
     * traffic flies at above the ground, so a deck that does not clear it is a wall
     * across the road rather than a bridge over it. Note this is a REFUSAL floor and not
     * the clearance a structure is designed for - the design is one whole deck height,
     * and what a real structure ends up with is the distribution
     * StructurePlacementReport.Clearances reports.
     */
    public static float MinDeckClearance => world.MetaGen.ClusterNavigationHeight;


    /**
     * One candidate corridor, complete enough to judge without touching the store.
     */
    private sealed class Candidate
    {
        internal StreetPoint Crossing;
        internal Stroke ArmA, ArmB;
        internal StreetPoint FootA, FootB;
        internal List<Stroke> Chain;

        internal Stroke RampA => Chain[0];
        internal Stroke Deck => Chain[1];
        internal Stroke RampB => Chain[2];

        /**
         * The arms the crossing keeps - the road that ends up underneath.
         */
        internal List<Stroke> Under;
    }


    /**
     * Lift what can be lifted.
     *
     * @param groundHeightOf
     *     The UNRELAXED ground under a junction, i.e. what TerrainStreetHeight answers.
     *     Null means no height is available, and then nothing is placed - loudly, because
     *     a policy that silently does nothing is the failure mode this whole work stream
     *     keeps hitting.
     */
    public static StructurePlacementReport Place(
        StrokeStore store, int clusterId, GradePolicy policy,
        Func<StreetPoint, float> groundHeightOf,
        float rampClearance, float minSpanLength, float maxSpanLength)
    {
        var report = new StructurePlacementReport();

        if (null == groundHeightOf)
        {
            Warning(_dc,
                "Grade separation is enabled but no ground height source was supplied, so "
                + "no corridor can be judged and no structure is placed.");
            return report;
        }

        var candidates = _findCandidates(
            store, clusterId, policy, rampClearance, minSpanLength, maxSpanLength, report);

        /*
         * ⚠️ THE FIXED POINT, and it is what makes the refusal exact rather than close.
         *
         * The anchor heights are computed over the ground strokes the FINISHED network
         * will have, which is today's strokes minus the arms of every corridor that ends
         * up lifted. Refusing one puts its two arms back, which changes those heights for
         * everything else - so the set is re-measured until it stops shrinking. It does
         * stop: a round either refuses nothing, or removes at least one candidate from a
         * finite set, and a refused candidate is never reconsidered.
         */
        var selected = new List<Candidate>(candidates);
        Dictionary<int, float> heights;

        while (true)
        {
            heights = _anchorHeights(store, selected, groundHeightOf, policy);

            var refusedNow = new List<Candidate>();
            foreach (var c in selected)
            {
                var reason = _judgeHeights(c, heights, policy);
                if (null != reason)
                {
                    report.Refuse(reason.Value);
                    refusedNow.Add(c);
                }
            }

            if (0 == refusedNow.Count)
            {
                break;
            }

            foreach (var c in refusedNow)
            {
                selected.Remove(c);
            }
        }

        /*
         * The heights the game will answer with: the anchor above, plus every accepted
         * structure's designed profile written over it. Exactly GradeRelaxer.Relax's own
         * two steps, in its own order, on the network that is about to exist.
         *
         * ⚠️ MEASURED BEFORE THE COMMIT, and that is not a preference. A junction carries
         * a process-global provisional id until StrokeStore._assignLocalId gives it the
         * network's own, so a deck junction is a DIFFERENT key before and after its chain
         * is added - and a height table built from the one cannot be read with the other.
         */
        var designed = new Dictionary<int, float>(heights);
        var measured = new List<(float DeckGrade, List<float> Clearances)>();

        foreach (var c in selected)
        {
            StructureProfile.Design(c.Chain, designed, policy);
        }

        foreach (var c in selected)
        {
            measured.Add((RoadGradeOf(c.Deck, designed), _clearancesUnder(c, designed).ToList()));
        }

        var builder = new NetworkBuilder(store);

        for (int i = 0; i < selected.Count; ++i)
        {
            Candidate c = selected[i];

            /*
             * The arms first. CommitChain refuses a chain whose members are already in a
             * store and checks every level before it adds anything, so a structure is
             * either whole or absent - but the road it replaces has to be gone before the
             * road that replaces it arrives, or the two are momentarily both there.
             */
            store.Remove(c.ArmA);
            store.Remove(c.ArmB);
            builder.CommitChain(c.Chain);

            ++report.Placed;
            report.DeckGrades.Add(measured[i].DeckGrade);
            report.Clearances.AddRange(measured[i].Clearances);
        }

        return report;
    }


    /*
     * ------------------------------------------------------ finding corridors --------
     */

    private static List<Candidate> _findCandidates(
        StrokeStore store, int clusterId, GradePolicy policy,
        float rampClearance, float minSpanLength, float maxSpanLength,
        StructurePlacementReport report)
    {
        var accepted = new List<Candidate>();

        /*
         * Junctions taken by an accepted structure, and strokes it removes or adds. Two
         * candidates sharing either would each be built on the other's assumption about
         * what is there.
         */
        var claimedPoints = new HashSet<int>();
        var claimedStrokes = new HashSet<Stroke>();

        int componentsNow = _componentsAfter(store, null, claimedStrokes, accepted);
        int nextFreeId = NextFreeIdIn(store);

        /*
         * By junction id, so that which corridor wins a conflict does not depend on the
         * order GetStreetPoints happens to be in.
         */
        foreach (var m in store.GetStreetPoints().OrderBy(sp => sp.Id))
        {
            var pair = _straightThroughPairAt(m);
            if (null == pair)
            {
                continue;
            }

            ++report.Considered;

            (Stroke armA, Stroke armB) = pair.Value;
            var under = m.GetAngleArray().Where(s => s != armA && s != armB).ToList();

            if (under.Count < 2)
            {
                report.Refuse(StructureRefusal.InteriorTBranch);
                continue;
            }

            if (claimedPoints.Contains(m.Id)
                || claimedStrokes.Contains(armA) || claimedStrokes.Contains(armB))
            {
                report.Refuse(StructureRefusal.OverlapsAStructure);
                continue;
            }

            StreetPoint footA = armA.A == m ? armA.B : armA.A;
            StreetPoint footB = armB.A == m ? armB.B : armB.A;

            if (footA == footB
                || claimedPoints.Contains(footA.Id) || claimedPoints.Contains(footB.Id))
            {
                report.Refuse(StructureRefusal.OverlapsAStructure);
                continue;
            }

            float rampLength =
                OverpassBuilder.RampLengthFor(policy, m.Level, StrokeKind.Bridge);

            /*
             * The deck has to reach past the junction it flies over, and by enough that
             * the ramps keep the plan separation from the road underneath that
             * ClearanceConstraint demands of every ordinary street. Otherwise the
             * structure would be laid in a position the generator itself would have
             * refused to grow a road into.
             */
            float overhang = rampClearance;

            if (armA.Length < rampLength + overhang || armB.Length < rampLength + overhang)
            {
                report.Refuse(StructureRefusal.ArmTooShort);
                continue;
            }

            /*
             * IsPrimary is an orientation bit and not a rank (§0.4), so it is carried off
             * the road being replaced rather than asserted.
             */
            var chain = new OverpassBuilder(clusterId).Build(
                footA, footB, StrokeKind.Bridge, rampLength,
                Single.Max(armA.Weight, armB.Weight), armA.IsPrimary);

            if (null == chain)
            {
                report.Refuse(StructureRefusal.NotAStructure);
                continue;
            }

            /*
             * Before anything reads a height out of a table keyed on a junction id.
             * Advanced even for a chain that is then refused, so that two candidates
             * cannot be handed the same numbers.
             */
            nextFreeId = ReserveIdsIn(store, chain, nextFreeId);

            var candidate = new Candidate
            {
                Crossing = m, ArmA = armA, ArmB = armB,
                FootA = footA, FootB = footB, Chain = chain, Under = under
            };

            float deckLength = candidate.Deck.Length;
            if (deckLength < minSpanLength
                || (maxSpanLength > 0f && deckLength > maxSpanLength))
            {
                report.Refuse(StructureRefusal.SpanLength);
                continue;
            }

            /*
             * ⚠️ The corridor bends at its crossing and the structure does not: the chain
             * runs straight from foot to foot. So whether the deck actually passes over
             * the road underneath is a MEASUREMENT, not a consequence of having chosen a
             * nearly straight pair.
             */
            if (!under.Any(s => null != candidate.Deck.Intersects(s)))
            {
                report.Refuse(StructureRefusal.MissesWhatItCrosses);
                continue;
            }

            if (!_rampsAreClear(
                    store, candidate, rampClearance, claimedStrokes, accepted))
            {
                report.Refuse(StructureRefusal.RampClearance);
                continue;
            }

            /*
             * ⚠️ Last, because it is the most expensive and because it is a property of
             * the network rather than of the corridor.
             */
            int after = _componentsAfter(store, candidate, claimedStrokes, accepted);
            if (after > componentsNow)
            {
                report.Refuse(StructureRefusal.WouldDisconnect);
                continue;
            }

            componentsNow = after;
            accepted.Add(candidate);

            claimedPoints.Add(m.Id);
            claimedPoints.Add(footA.Id);
            claimedPoints.Add(footB.Id);
            claimedStrokes.Add(armA);
            claimedStrokes.Add(armB);
        }

        return accepted;
    }


    /**
     * ⚠️ Give every junction a chain invented an id no junction of this network has.
     *
     * A StreetPoint carries a PROCESS-GLOBAL provisional id until StrokeStore hands it
     * the network's own, and a network's own ids start at 1 - so a deck end and a real
     * junction can carry the same number. Everything below judges heights out of a
     * Dictionary keyed on that id, so a collision does not fail: it silently answers one
     * junction's question with another junction's height.
     *
     * The ids handed out are the ones the store itself would hand out, continuing from
     * the highest it has issued, which is also what makes them unique among themselves.
     * They are overwritten by _assignLocalId the moment a chain is committed; this is
     * only about the table being keyed correctly before that.
     *
     * ⚠️ ASSERTED AS EQUALITY WITH THOSE IDS, and it has to be. The property - no
     * invented junction borrows an existing one's identity - is satisfied by luck
     * whenever the process-global counter happens to be above the network's range, which
     * in a test host that has already built a dozen cities it always is. So a test for
     * the absence of a collision cannot tell this rule from that luck, exactly as §7p's
     * containment test could not tell a guess from a refusal, and the gate is on the
     * numbers themselves.
     *
     * @param nextFree
     *     The lowest id this network has not issued, carried across chains.
     * @returns
     *     The same, advanced past the junctions this chain invented.
     */
    internal static int ReserveIdsIn(StrokeStore store, IReadOnlyList<Stroke> chain, int nextFree)
    {
        /*
         * By reference, because a chain's members SHARE their junctions - the deck's two
         * ends are the ramps' far ends - and numbering one twice would give the two
         * strokes that meet there two different ideas of where they meet.
         */
        var given = new HashSet<StreetPoint>();

        foreach (var s in chain)
        {
            foreach (var sp in new[] { s.A, s.B })
            {
                if (sp.InStore || !given.Add(sp))
                {
                    continue;
                }

                sp.Id = nextFree++;
            }
        }

        return nextFree;
    }


    /**
     * The lowest id this network has not issued.
     */
    internal static int NextFreeIdIn(StrokeStore store)
    {
        int next = 1;
        foreach (var sp in store.GetStreetPoints())
        {
            if (sp.Id >= next) next = sp.Id + 1;
        }

        return next;
    }


    /**
     * The pair of ordinary ground streets at this junction that come closest to running
     * straight through it, or null when there is none.
     */
    private static (Stroke, Stroke)? _straightThroughPairAt(StreetPoint m)
    {
        var arms = m.GetAngleArray();
        if (null == arms || arms.Count < 3)
        {
            /*
             * Two arms is a bend in a road, not a crossing: there would be nothing under
             * the deck. Fewer is an end.
             */
            return null;
        }

        Stroke bestA = null, bestB = null;
        float bestDot = MinStraightDot;

        for (int i = 0; i < arms.Count; ++i)
        {
            for (int j = i + 1; j < arms.Count; ++j)
            {
                Stroke a = arms[i], b = arms[j];
                if (!_isOrdinaryGroundStreet(a, m) || !_isOrdinaryGroundStreet(b, m))
                {
                    continue;
                }

                StreetPoint pa = a.A == m ? a.B : a.A;
                StreetPoint pb = b.A == m ? b.B : b.A;
                if (pa == pb)
                {
                    continue;
                }

                Vector2 da = pa.Pos - m.Pos;
                Vector2 db = pb.Pos - m.Pos;
                if (!(da.LengthSquared() > 0f) || !(db.LengthSquared() > 0f))
                {
                    continue;
                }

                float dot = Vector2.Dot(Vector2.Normalize(da), Vector2.Normalize(db));
                if (dot <= bestDot)
                {
                    bestDot = dot;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        return null == bestA ? null : (bestA, bestB);
    }


    /**
     * A ConnectorBridge is an ordinary ground road and exists in one to three copies in
     * every shipped city (§0.7), but it is the network's repair rather than a street the
     * ruleset laid, and a structure has no business rebuilding one. A structure member is
     * excluded outright.
     */
    private static bool _isOrdinaryGroundStreet(Stroke s, StreetPoint m)
        => s.Kind == StrokeKind.Street && s.Level == m.Level;


    /**
     * No road may come within RampClearance of either ramp, and none may cross one.
     *
     * The same rule ClearanceConstraint applies to every candidate street the generator
     * grows, applied in the other direction - and through the same store query, so the
     * two cannot come to disagree about what "near" means.
     */
    private static bool _rampsAreClear(
        StrokeStore store, Candidate candidate, float rampClearance,
        HashSet<Stroke> claimedStrokes, List<Candidate> accepted)
    {
        if (!(rampClearance > 0f))
        {
            return true;
        }

        foreach (var ramp in new[] { candidate.RampA, candidate.RampB })
        {
            foreach (var other in store.GetStrokesNear(ramp, rampClearance))
            {
                if (other == candidate.ArmA || other == candidate.ArmB
                    || claimedStrokes.Contains(other))
                {
                    /*
                     * On its way out with this structure or with an earlier one.
                     */
                    continue;
                }

                if (_sharesAJunction(ramp, other))
                {
                    /*
                     * Sharing a junction with the ramp is how you get onto it.
                     */
                    continue;
                }

                return false;
            }

            /*
             * Structures accepted earlier in this pass are not in the store yet, so the
             * query above cannot see them.
             */
            foreach (var earlier in accepted)
            {
                foreach (var member in earlier.Chain)
                {
                    if (_sharesAJunction(ramp, member))
                    {
                        continue;
                    }

                    if (ramp.Distance(member.A.Pos) <= rampClearance
                        || ramp.Distance(member.B.Pos) <= rampClearance
                        || member.Distance(ramp.A.Pos) <= rampClearance
                        || member.Distance(ramp.B.Pos) <= rampClearance
                        || null != member.Intersects(ramp))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }


    private static bool _sharesAJunction(Stroke x, Stroke y)
        => x.A == y.A || x.A == y.B || x.B == y.A || x.B == y.B;


    /**
     * How many pieces the network would be in with this corridor lifted as well.
     *
     * ⚠️ A LIFT CAN TAKE A CITY APART, and it took measuring to find out: on the shipped
     * terrain every one of the seven seeds stays in one piece with its structures on it,
     * so real terrain data says nothing here at all - but the same seed017@2400 on LEVEL
     * ground, where the deck grade refuses nothing and fifty structures go in, comes out
     * in two pieces without this rule. §9's "a rule can be invisible to unlimited real
     * data", one more time.
     *
     * The reason is what a grade separated crossing IS: there are no slip roads, so the
     * through road and the road beneath it stop being connected to each other at that
     * point. Two removed edges and one new path between their far ends; usually the city
     * closes over it, and this is the test of whether it did.
     *
     * A chain contributes exactly one edge here - foot to foot - because its own two deck
     * junctions are interior to it and reachable from nowhere else.
     *
     * @param candidate
     *     The corridor being considered, or null for the network as it stands.
     */
    private static int _componentsAfter(
        StrokeStore store, Candidate candidate, HashSet<Stroke> claimedStrokes,
        List<Candidate> accepted)
    {
        var adjacency = new Dictionary<int, List<int>>();

        void link(int a, int b)
        {
            if (!adjacency.TryGetValue(a, out var la)) adjacency[a] = la = new List<int>();
            if (!adjacency.TryGetValue(b, out var lb)) adjacency[b] = lb = new List<int>();
            la.Add(b);
            lb.Add(a);
        }

        foreach (var sp in store.GetStreetPoints())
        {
            adjacency[sp.Id] = new List<int>();
        }

        foreach (var s in store.GetStrokes())
        {
            if (claimedStrokes.Contains(s)) continue;
            if (null != candidate && (s == candidate.ArmA || s == candidate.ArmB)) continue;

            link(s.A.Id, s.B.Id);
        }

        foreach (var c in accepted)
        {
            link(c.FootA.Id, c.FootB.Id);
        }

        if (null != candidate)
        {
            link(candidate.FootA.Id, candidate.FootB.Id);
        }

        var seen = new HashSet<int>();
        int components = 0;

        foreach (int start in adjacency.Keys)
        {
            if (!seen.Add(start)) continue;

            ++components;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                foreach (int next in adjacency[queue.Dequeue()])
                {
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
        }

        return components;
    }


    /*
     * ------------------------------------------------------------- the heights -------
     */

    /**
     * The heights the finished city will have at every junction that is not part of a
     * structure.
     *
     * This is GradeRelaxer's own anchor pass, run here rather than reimplemented: the
     * ground network it relaxes is exactly the one the finished store will have, because
     * every selected corridor's two arms are taken out of the list and a chain adds
     * nothing but structures, which the anchor pass filters out anyway.
     */
    private static Dictionary<int, float> _anchorHeights(
        StrokeStore store, List<Candidate> selected,
        Func<StreetPoint, float> groundHeightOf, GradePolicy policy)
    {
        var lifted = new HashSet<Stroke>();
        foreach (var c in selected)
        {
            lifted.Add(c.ArmA);
            lifted.Add(c.ArmB);
        }

        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = groundHeightOf(sp);
        }

        GradeRelaxer.Relax(
            store.GetStrokes().Where(s => !lifted.Contains(s)).ToList(), heights, policy);

        return heights;
    }


    /**
     * ⚠️ The buildability criterion, and it is the MEASURED grades of the designed chain.
     *
     * §2's corridor-fit table asks whether two ramps fit in the end spans. It cannot see
     * the deck at all, and the deck is where the trouble is (§10.5). So the chain is
     * designed by StructureProfile - the production expression, not a restatement of it -
     * and then measured: both ramps, because a design that silently refused a malformed
     * ramp leaves it at whatever the terrain gave it, and the deck, because nothing
     * anywhere bounds it.
     */
    private static StructureRefusal? _judgeHeights(
        Candidate c, Dictionary<int, float> heights, GradePolicy policy)
    {
        var designed = new Dictionary<int, float>(heights);
        StructureProfile.Design(c.Chain, designed, policy);

        var refusal = JudgeGradesOf(c.Chain, designed, policy);
        if (null != refusal)
        {
            return refusal;
        }

        foreach (float clearance in _clearancesUnder(c, designed))
        {
            if (clearance < MinDeckClearance)
            {
                return StructureRefusal.DeckClearance;
            }
        }

        return null;
    }


    /**
     * Both grades of an already designed chain, and the answer to "may this be built".
     *
     * Separate from its caller so that it can be driven directly. The ramp half is a
     * BACKSTOP and is unreachable through OverpassBuilder - every chain that builder
     * produces is well formed, so StructureProfile designs both ramps exactly and the
     * measurement is 10.000 % by construction. What it catches is a chain the profile
     * REFUSED, which leaves the deck end at whatever the terrain gave it: a ramp that
     * does not change level, or two ramps claiming one deck junction. §10.6's mutation 9
     * is the same shape - a deliberately malformed structure is the only thing that can
     * tell "designed and measured" apart from "assumed".
     *
     * @param chain
     *     ramp, deck, ramp - what OverpassBuilder.Build returns.
     * @param designed
     *     Ground heights INCLUDING whatever StructureProfile.Design wrote.
     */
    internal static StructureRefusal? JudgeGradesOf(
        IReadOnlyList<Stroke> chain, Dictionary<int, float> designed, GradePolicy policy)
    {
        foreach (var ramp in new[] { chain[0], chain[2] })
        {
            float grade = Single.Abs(RoadGradeOf(ramp, designed));
            if (grade > policy.MaxGradeFor(ramp) + 1e-4f)
            {
                return StructureRefusal.RampGrade;
            }
        }

        float deckGrade = Single.Abs(RoadGradeOf(chain[1], designed));
        if (deckGrade > policy.MaxDeckGradeFor(chain[1]))
        {
            return StructureRefusal.DeckGrade;
        }

        return null;
    }


    /**
     * Rise over run of a stroke's ROAD - ground plus the elevation of whichever deck each
     * end is on.
     *
     * ⚠️ The two terms are the whole point, and one of them hides. A ramp joins two decks,
     * so dropping LevelElevation understates its grade by a whole deck height over its own
     * length - which at MaxRampGrade is the entire grade. A DECK, on the other hand, has
     * both ends on one level, so the term cancels there and every measurement of a deck
     * agrees whether or not it is present. A gate built only on decks therefore cannot see
     * the difference, which is why this is internal and driven on a ramp directly.
     */
    internal static float RoadGradeOf(Stroke s, Dictionary<int, float> heights)
    {
        float length = s.Length;
        if (!(length > 0.001f))
        {
            return 0f;
        }

        float rise = (heights[s.B.Id] + s.B.LevelElevation)
                     - (heights[s.A.Id] + s.A.LevelElevation);

        return rise / length;
    }


    /**
     * How far the deck stands over each road that passes under it, at the point it
     * crosses.
     */
    private static IEnumerable<float> _clearancesUnder(
        Candidate c, Dictionary<int, float> heights)
    {
        Stroke deck = c.Deck;

        foreach (var other in c.Under)
        {
            var si = deck.Intersects(other);
            if (null == si)
            {
                continue;
            }

            /*
             * Intersects reports the parameter along the receiver as ScaleExists and the
             * one along its argument as ScaleCand.
             */
            float onDeck = _roadHeightAlong(deck, si.ScaleExists, heights);
            float onGround = _roadHeightAlong(other, si.ScaleCand, heights);

            yield return onDeck - onGround;
        }
    }


    private static float _roadHeightAlong(Stroke s, float t, Dictionary<int, float> heights)
    {
        float a = heights[s.A.Id] + s.A.LevelElevation;
        float b = heights[s.B.Id] + s.B.LevelElevation;

        return a + t * (b - a);
    }
}
