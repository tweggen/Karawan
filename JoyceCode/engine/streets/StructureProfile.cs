using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.streets;


/**
 * Where a grade separated structure stands, in height.
 *
 * A ramp, a bridge deck and a tunnel bore are not roads that happen to be somewhere;
 * they are built to a profile. So the profile is DESIGNED here, exactly, and everything
 * else gives way to it: GradeRelaxer treats the junctions this pass writes as boundary
 * conditions and moves their neighbours instead, and
 * ClusterConformElevationOperator grades the ground toward the result. That is §3b
 * option B of STREETS-3D-PHASE-B-CROSSING-POLICY, and it is the same shape as the rest
 * of this workstream: the road is designed and the ground conforms, not the ground
 * bending the road.
 *
 * ## The one arithmetic point that matters
 *
 * The table this pass writes into is GROUND height - what IStreetHeightSource answers,
 * and what StreetHeightField grades the terrain toward. A junction's ROAD stands
 * StreetPoint.LevelElevation above that, so a ramp's grade is a statement about
 * `ground + LevelElevation` at each of its ends and never about ground alone. Designing
 * on ground alone is how a bridge comes out with a ramp of the right number and the
 * wrong slope: the two agree perfectly until the first junction that is not on level 0.
 *
 * Deliberately NOT grading the ground to the deck itself. The field is built from ground
 * heights, so the terrain under a bridge is graded toward the ground under the bridge -
 * a berm up to the deck is exactly what an underpass must not have.
 *
 * ## What is designed, and what is only anchored
 *
 * Only a RAMP is designed, and what is designed about it is the height of the end it
 * lifts. The other end - the foot - is read and never written: it is a junction of the
 * ordinary city and stands where the city stands, which GradeRelaxer's anchor pass has
 * settled before this runs. A bridge deck or a tunnel bore is designed by neither; it
 * spans between two junctions its own ramps placed, and its grade is whatever the two
 * feet make it. That grade is NOT bounded here and can be far steeper than a ramp -
 * measured at 29 % on a real corridor - which is a corridor for the placement policy to
 * refuse rather than a number for this pass to invent.
 */
public static class StructureProfile
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;


    /**
     * Shared empty answer for the overwhelmingly common case - every city the shipped
     * ruleset builds - so that a network with no structure in it allocates nothing and
     * takes the identical path it always took.
     */
    private static readonly HashSet<int> _none = new();


    /**
     * Every junction a Ramp, Bridge or Tunnel touches - the ones the relaxation may not
     * move.
     *
     * The feet are in here as well as the deck junctions. A foot is where the designed
     * profile meets the ordinary city, so if it were free the relaxation would move it
     * and take the ramp's grade with it; pinning only the deck ends would design a
     * structure and then let the sweep bend it.
     *
     * @returns
     *     Empty - and the same empty set every time - when the network has no structure
     *     in it, which is every city the shipped ruleset builds.
     */
    public static HashSet<int> PinnedJunctionsOf(IEnumerable<Stroke> strokes)
    {
        HashSet<int> pinned = null;

        foreach (var s in strokes)
        {
            if (!StrokeKinds.IsStructure(s.Kind))
            {
                continue;
            }

            pinned ??= new HashSet<int>();
            pinned.Add(s.A.Id);
            pinned.Add(s.B.Id);
        }

        return pinned ?? _none;
    }


    /**
     * Give the structures in this network their designed heights.
     *
     * @param ordered
     *     Every stroke of the network, already ordered by Sid. The order is load
     *     bearing: where two ramps disagree about one deck junction the first by Sid
     *     wins, and a city is regenerated from a seed rather than stored.
     * @param heights
     *     Ground height per junction id, modified in place. A structure's FEET are read
     *     and never written - they stand where the city without the structure stands,
     *     which is what GradeRelaxer's anchor pass has already settled - and its deck
     *     junctions are overwritten with the height that gives the ramp its grade.
     * @param policy
     *     Asked how steep a ramp may be, through the same MaxGradeFor every other
     *     stroke goes through, so that there is one expression for "how steep may this
     *     be" rather than two that agree until somebody tunes one of them.
     */
    public static void Design(
        IEnumerable<Stroke> ordered, Dictionary<int, float> heights, GradePolicy policy)
    {
        var designed = new HashSet<int>();
        int nMalformed = 0;

        foreach (var s in ordered)
        {
            /*
             * Only a ramp has a designed height, because only a ramp changes level. A
             * bridge deck or a tunnel bore spans between two junctions its own ramps
             * have already placed, and its grade is whatever those two feet make it -
             * which is a question for the placement policy to refuse, not for this pass
             * to invent an answer to.
             */
            if (s.Kind != StrokeKind.Ramp)
            {
                continue;
            }

            /*
             * OverpassBuilder records a ramp on the deck it LEAVES from, so the foot is
             * the endpoint whose own level is the ramp's. Exactly one end may match: a
             * ramp both of whose ends are on the ramp's level does not climb, and one
             * with neither is filed on a deck it does not touch.
             */
            bool aIsFoot = s.A.Level == s.Level;
            bool bIsFoot = s.B.Level == s.Level;
            if (aIsFoot == bIsFoot)
            {
                ++nMalformed;
                continue;
            }

            StreetPoint foot = aIsFoot ? s.A : s.B;
            StreetPoint deck = aIsFoot ? s.B : s.A;

            float length = s.Length;
            if (!(length > 0.001f))
            {
                ++nMalformed;
                continue;
            }

            if (!heights.TryGetValue(foot.Id, out float footGround))
            {
                ++nMalformed;
                continue;
            }

            if (!designed.Add(deck.Id))
            {
                /*
                 * Two ramps arriving at one junction design it from two different feet
                 * and in general disagree. The first by Sid keeps it, so the answer does
                 * not depend on enumeration order, and the disagreement is reported
                 * rather than silently resolved.
                 */
                ++nMalformed;
                continue;
            }

            float climb = policy.MaxGradeFor(s) * length;
            if (deck.Level < foot.Level)
            {
                climb = -climb;
            }

            /*
             * The road at the foot is footGround + foot.LevelElevation; the road at the
             * deck end is that plus the climb; the ground under the deck end is that
             * less its own deck elevation.
             */
            heights[deck.Id] =
                footGround + foot.LevelElevation + climb - deck.LevelElevation;
        }

        if (nMalformed > 0)
        {
            Warning(_dc,
                $"{nMalformed} ramps could not be given a designed profile and were left "
                + "at whatever height the terrain gave them - a ramp that does not join "
                + "two decks, or two ramps arriving at one junction.");
        }
    }
}
