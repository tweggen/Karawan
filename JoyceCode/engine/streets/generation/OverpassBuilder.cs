using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * Builds a ramp - deck - ramp structure between two junctions on one deck.
 *
 * The plan view is unchanged: the structure runs straight from `from` to `to`, exactly
 * where an ordinary street would have run. What differs is that its middle section
 * sits one level up (a bridge) or one level down (a tunnel), so it passes whatever is
 * in between without meeting it.
 *
 *        from ----ramp---- o======deck======o ----ramp---- to      level +/-1
 *        level L           ^                ^              level L
 *                          the ramps are the only strokes joining two levels
 *
 * Nothing here touches the store. The caller validates the proposed chain through the
 * constraint pipeline and then commits it atomically with
 * NetworkBuilder.CommitChain, so a structure that fails anywhere leaves nothing
 * behind. A half-built bridge is worse than no bridge.
 */
internal sealed class OverpassBuilder
{
    private readonly int _clusterId;


    internal OverpassBuilder(int clusterId)
    {
        _clusterId = clusterId;
    }


    /**
     * Which deck a structure of this kind puts its span on, relative to its feet.
     */
    internal static sbyte DeckLevelOf(sbyte groundLevel, StrokeKind deckKind)
        => deckKind == StrokeKind.Tunnel
            ? (sbyte)(groundLevel - 1)
            : (sbyte)(groundLevel + 1);


    /**
     * How long each ramp of such a structure has to be.
     *
     * A ramp is the one stroke whose grade is designed rather than inherited, so its
     * length is not a fraction of anything: it is exactly the climb divided by the grade
     * the policy builds ramps at. Build used to take a fraction of the run instead,
     * which makes a long corridor's ramps steeper than a short one's - the opposite of
     * what a design is.
     *
     * Phrased in StreetPoint.LevelElevation's own expression rather than in the deck
     * height, because how high a deck stands is StreetLevels.ElevationOf and is allowed
     * exactly one copy (WP-B1.7). That also makes this right for a tunnel, whose climb
     * is downwards, and for a structure between any two adjacent decks.
     */
    internal static float RampLengthFor(GradePolicy policy, sbyte groundLevel, StrokeKind deckKind)
    {
        sbyte deckLevel = DeckLevelOf(groundLevel, deckKind);

        return Single.Abs(
                   StreetLevels.ElevationOf(deckLevel) - StreetLevels.ElevationOf(groundLevel))
               / policy.MaxRampGrade;
    }


    /**
     * @param from, to
     *     Junctions on the same deck. Both may already be in the store.
     * @param deckKind
     *     StrokeKind.Bridge to go over, StrokeKind.Tunnel to go under.
     * @param rampLength
     *     How long each ramp is, in metres - RampLengthFor. Both ramps together must
     *     leave a deck between them.
     * @param weight
     *     Carried onto every member of the chain.
     * @param isPrimary
     *     The orientation bit of the road this structure replaces. NOT hierarchy:
     *     Stroke.IsPrimary is a "primary or secondary direction" that SuccessorEmitter
     *     flips per branch, and 46-66 % of a city's strokes carry it (§0.4). It used to
     *     be hard-coded true here, so a structure asserted something about the road it
     *     was built from rather than carrying it.
     * @returns
     *     Three unattached strokes: ramp, deck, ramp. Null when the arguments cannot
     *     describe a structure at all.
     */
    internal List<Stroke> Build(
        StreetPoint from, StreetPoint to, StrokeKind deckKind, float rampLength, float weight,
        bool isPrimary)
    {
        if (null == from || null == to)
        {
            return null;
        }

        if (deckKind != StrokeKind.Bridge && deckKind != StrokeKind.Tunnel)
        {
            /*
             * The span is what makes this a structure. A chain whose middle member is an
             * ordinary street would be two ramps climbing to nothing.
             */
            return null;
        }

        if (from.Level != to.Level)
        {
            /*
             * Both feet of the structure stand on the same deck; a run that already
             * changes level is a different thing entirely.
             */
            return null;
        }

        Vector2 span = to.Pos - from.Pos;
        float run = span.Length();

        if (!(rampLength > 0f) || !(run > 0f))
        {
            return null;
        }

        float rampFraction = rampLength / run;
        if (rampFraction >= 0.5f)
        {
            /*
             * The two ramps would meet or overlap, which leaves no deck between them.
             */
            return null;
        }

        sbyte groundLevel = from.Level;
        sbyte deckLevel = DeckLevelOf(groundLevel, deckKind);

        var deckStart = new StreetPoint() { ClusterId = _clusterId, Level = deckLevel };
        deckStart.SetPos(from.Pos + span * rampFraction);
        deckStart.PushCreator("overpass_deck_start");

        var deckEnd = new StreetPoint() { ClusterId = _clusterId, Level = deckLevel };
        deckEnd.SetPos(from.Pos + span * (1f - rampFraction));
        deckEnd.PushCreator("overpass_deck_end");

        /*
         * StreetPoint.SetPos quantises to 10 cm, so a very short structure can end up
         * with its two deck points on the same spot. That is not a bridge.
         */
        if (deckStart.Pos == deckEnd.Pos)
        {
            return null;
        }

        return new List<Stroke>
        {
            _member(from, deckStart, StrokeKind.Ramp, groundLevel, weight, isPrimary,
                "overpass_ramp_up"),
            _member(deckStart, deckEnd, deckKind, deckLevel, weight, isPrimary,
                "overpass_deck"),
            _member(deckEnd, to, StrokeKind.Ramp, groundLevel, weight, isPrimary,
                "overpass_ramp_down"),
        };
    }


    private Stroke _member(
        StreetPoint a, StreetPoint b, StrokeKind kind, sbyte level, float weight, bool isPrimary,
        string creator)
    {
        var stroke = new Stroke()
        {
            ClusterId = _clusterId,
            IsPrimary = isPrimary,
            Weight = weight,
            Kind = kind,

            /*
             * A ramp is recorded on the deck it leaves from, so that level-filtered
             * queries on the ground still see it coming.
             */
            Level = level
        };

        stroke.A = a;
        stroke.B = b;
        stroke.PushCreator(creator);
        return stroke;
    }
}
