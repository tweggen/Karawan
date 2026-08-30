using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace engine.streets;


/**
 * Where the street network wants the ground to be, at an arbitrary point in plan.
 *
 * The companion query to IStreetHeightSource, which answers only at junctions. The
 * conforming elevation pass has an elevation sample rather than a junction, so it needs
 * the same question asked positionally - and it needs an answer that fades out, because
 * the terrain away from the roads is nobody's business but the terrain's.
 *
 * **Not a corridor.** §2c of the design originally described flattening a band roughly
 * streetWidth + shoulder wide along each stroke. That is not achievable on the grid this
 * runs on: MetaGen.GroundResolution = 20 over MetaGen.FragmentSize = 400 is one
 * elevation sample every 20 m, and a street is 8-22 m wide (Stroke.StreetWidth). A
 * corridor is therefore about ONE cell across, and cutting it produces blocky terracing
 * rather than a cutting - the grid cannot represent the shape being asked for. So this
 * grades the whole city SITE toward the street height field instead, which is a shape
 * the grid can carry. Real cuttings and embankments want a finer grid inside cities and
 * are deliberately a later, larger change.
 *
 * Pure by construction: a list of segments with a height at each end, and a distance
 * falloff. No terrain, no fragments, no engine, so the kernel and the query are tested
 * directly.
 */
public sealed class StreetHeightField
{
    /**
     * How far a street's influence reaches, in elevation cells.
     *
     * Three, and the number is a property of the GRID rather than of roads. Two things
     * have to be true at once. A falloff shorter than about two cells cannot be
     * rendered at all - there are no samples inside it to carry the ramp, and what comes
     * out is the step the corridor idea would have produced. And a skirt of a few tens
     * of metres is a plausible batter for the couple of metres of cut and fill
     * GradeRelaxer leaves behind, so three cells is not merely the smallest number that
     * works.
     *
     * Whoever refines the elevation grid inside cities should not simply inherit this:
     * at that point a real corridor becomes expressible and this whole class is what it
     * replaces.
     */
    public const float RadiusInCells = 3f;


    public static float DefaultRadius =>
        RadiusInCells * world.MetaGen.FragmentSize / world.MetaGen.GroundResolution;


    /**
     * One stroke, reduced to what the query needs: a segment, a height at each end, and
     * the segment's bounding box already grown by the radius so that the common case -
     * a sample nowhere near this street - costs four comparisons.
     */
    private readonly struct Span
    {
        public readonly Vector2 A;
        public readonly Vector2 AB;
        public readonly float LengthSquared;
        public readonly float HA;
        public readonly float HB;
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public Span(Vector2 a, Vector2 b, float hA, float hB, float radius)
        {
            A = a;
            AB = b - a;
            LengthSquared = AB.LengthSquared();
            HA = hA;
            HB = hB;
            MinX = Single.Min(a.X, b.X) - radius;
            MaxX = Single.Max(a.X, b.X) + radius;
            MinZ = Single.Min(a.Y, b.Y) - radius;
            MaxZ = Single.Max(a.Y, b.Y) + radius;
        }
    }


    private readonly Span[] _spans;
    private readonly float _radius;


    /**
     * How many strokes carried a height and are therefore in the field at all. Exposed
     * because "the field is empty" and "the field says nothing here" are different
     * answers and only the first is a problem worth reporting.
     */
    public int SpanCount => _spans.Length;


    /**
     * The blend kernel: how much a street standing this far away has to say about the
     * ground here.
     *
     * Smoothstep rather than a linear ramp, because its derivative is zero at BOTH ends.
     * At distance zero that means the road sits in a flat pad rather than at the apex of
     * a tent, and at the radius it means the graded ground leaves off tangentially
     * instead of creasing along a circle around every street. A crease at the edge of
     * the influence is exactly as visible as the terracing this pass exists to avoid,
     * and on this grid it would land on a single row of samples.
     *
     * @returns 1 on the centreline, 0 at and beyond the radius, monotonically falling in
     *     between.
     */
    public static float Falloff(float distance, float radius)
    {
        if (!(radius > 0f))
        {
            return distance <= 0f ? 1f : 0f;
        }

        if (distance <= 0f) return 1f;
        if (distance >= radius) return 0f;

        float t = 1f - distance / radius;
        return t * t * (3f - 2f * t);
    }


    /**
     * Move a terrain height toward what the streets want, by the influence they have.
     *
     * Separate from the query so that "how far does the ground move" is one expression
     * with a name.
     *
     * Full influence is a special case and no influence is not, which is the opposite way
     * round from what it looks like. Multiplying by zero is exact, so a sample outside
     * every street's reach comes back as its own terrain height with no branch at all.
     * A sample ON a road does not: a + 1 * (b - a) is not b once a and b are far apart,
     * and a road standing on ground that is nearly but not quite its own height is the
     * thing this whole pass exists to remove.
     */
    public static float Blend(float terrainHeight, float streetHeight, float influence)
    {
        if (influence >= 1f) return streetHeight;

        return terrainHeight + influence * (streetHeight - terrainHeight);
    }


    /**
     * What the street network says the ground should be at this point, and how strongly.
     *
     * @param p
     *     Position in the same space the strokes were built in - cluster relative, in the
     *     game.
     * @param height
     *     The streets' opinion: every stroke in range, weighted by the kernel, its own
     *     height taken at the point on it nearest p. A weighted MEAN rather than the
     *     nearest stroke's answer, because nearest-wins jumps as the winner changes and
     *     that seam runs down the middle of every block. Two strokes meeting at a
     *     junction agree there by construction, so the mean is exact where it matters
     *     most and only smooths between streets that genuinely differ.
     * @param influence
     *     The LARGEST single kernel weight, not the sum. The sum would exceed one wherever
     *     streets are dense and would have to be clamped, which reintroduces a hard edge
     *     along the clamp boundary; the largest weight is already in [0, 1] and reaches 1
     *     exactly on a road, which is the property that puts the road on the ground.
     * @returns
     *     false where no stroke is within the radius, in which case the terrain is left
     *     alone.
     */
    public bool TryHeightAt(in Vector2 p, out float height, out float influence)
    {
        float sumWeight = 0f;
        float sumWeighted = 0f;
        float largest = 0f;

        /*
         * Fixed order - the spans were sorted by Sid when the field was built - so the
         * floating point addition order is fixed too, and a fragment gets the same answer
         * however many times and from whichever thread it is recomputed.
         */
        for (int i = 0; i < _spans.Length; ++i)
        {
            ref readonly Span span = ref _spans[i];

            if (p.X < span.MinX || p.X > span.MaxX || p.Y < span.MinZ || p.Y > span.MaxZ)
            {
                continue;
            }

            float t = span.LengthSquared > 1e-6f
                ? Single.Clamp(Vector2.Dot(p - span.A, span.AB) / span.LengthSquared, 0f, 1f)
                : 0f;

            float distance = (p - (span.A + span.AB * t)).Length();
            float weight = Falloff(distance, _radius);
            if (weight <= 0f)
            {
                continue;
            }

            sumWeight += weight;
            sumWeighted += weight * (span.HA + t * (span.HB - span.HA));
            if (weight > largest) largest = weight;
        }

        if (sumWeight <= 0f)
        {
            height = 0f;
            influence = 0f;
            return false;
        }

        height = sumWeighted / sumWeight;
        influence = largest;
        return true;
    }


    /**
     * Build the field from a cluster's stroke graph.
     *
     * @param heightOf
     *     Ground height under a junction - IStreetHeightSource.GroundHeightAt in the
     *     game. Asked once per junction and reused for every stroke meeting there, so
     *     that the field inherits the one-junction-one-height invariant the network is
     *     built on rather than getting a second chance to disagree with it.
     */
    public static StreetHeightField Build(
        IEnumerable<Stroke> strokes, Func<StreetPoint, float> heightOf, float radius)
    {
        var heights = new Dictionary<int, float>();

        float heightAt(StreetPoint sp)
        {
            if (heights.TryGetValue(sp.Id, out float known)) return known;

            float h = heightOf(sp);
            heights[sp.Id] = h;
            return h;
        }

        /*
         * By Sid, exactly as GradeRelaxer orders its sweep. Cities are regenerated from a
         * seed rather than stored and elevation fragments are recomputed on demand, so
         * the answer may not depend on which fragment asked first or in what order the
         * store happens to enumerate.
         */
        var spans = strokes
            .Where(s => null != s.A && null != s.B)
            .OrderBy(s => s.Sid)
            .Select(s => new Span(s.A.Pos, s.B.Pos, heightAt(s.A), heightAt(s.B), radius))
            .ToArray();

        return new StreetHeightField(spans, radius);
    }


    private StreetHeightField(Span[] spans, float radius)
    {
        _spans = spans;
        _radius = radius;
    }
}
