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
     * @param from, to
     *     Junctions on the same deck. Both may already be in the store.
     * @param deckKind
     *     StrokeKind.Bridge to go over, StrokeKind.Tunnel to go under.
     * @param rampFraction
     *     How much of the total run each ramp takes, 0..0.5. The deck gets the rest.
     * @param weight
     *     Carried onto every member of the chain.
     * @returns
     *     Three unattached strokes: ramp, deck, ramp. Null when the arguments cannot
     *     describe a structure at all.
     */
    internal List<Stroke> Build(
        StreetPoint from, StreetPoint to, StrokeKind deckKind, float rampFraction, float weight)
    {
        if (null == from || null == to)
        {
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

        if (rampFraction <= 0f || rampFraction >= 0.5f)
        {
            return null;
        }

        sbyte groundLevel = from.Level;
        sbyte deckLevel = deckKind == StrokeKind.Tunnel
            ? (sbyte)(groundLevel - 1)
            : (sbyte)(groundLevel + 1);

        Vector2 span = to.Pos - from.Pos;

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
            _member(from, deckStart, StrokeKind.Ramp, groundLevel, weight, "overpass_ramp_up"),
            _member(deckStart, deckEnd, deckKind, deckLevel, weight, "overpass_deck"),
            _member(deckEnd, to, StrokeKind.Ramp, groundLevel, weight, "overpass_ramp_down"),
        };
    }


    private Stroke _member(
        StreetPoint a, StreetPoint b, StrokeKind kind, sbyte level, float weight, string creator)
    {
        var stroke = new Stroke()
        {
            ClusterId = _clusterId,
            IsPrimary = true,
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
