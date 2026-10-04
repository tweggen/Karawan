using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using static engine.Logger;

namespace engine.streets.generation;


/**
 * The surface a city block's pavement actually is, as the block floor's own cap.
 *
 * ⚠️ WHY THIS EXISTS, AND WHY A BOUND OVER THE BLOCK'S CORNERS IS NOT ENOUGH ANY MORE.
 * BuildingFooting founds a building on a single scalar - the footprint goes to the L-system
 * with Y forced to zero and is extruded straight up - so that scalar has to be at or below
 * the floor everywhere over the footprint. Until WP-O2 a block carried at most one building
 * whose footprint was the block inset by a pavement width, so the block's LOWEST CORNER was
 * both a sound bound (every cap vertex carries a corner height or a blend of two of one
 * edge's pair) and a tight one (0.19-0.61 m below the exact footprint minimum at the
 * median). WP-O2 gives every piece of a block's buildable land its own building, and the
 * block-wide bound then buries the higher piece by a median 6-7 m and up to 28 m (§7t.10.7).
 *
 * ⚠️ AND THE OBVIOUS PER-PIECE BOUND - the lowest of the piece's own boundary heights,
 * read off the block's boundary ring the way BuildingFooting.GroundAt reads it - IS NOT A
 * BOUND. Measured over the four baseline cities on the shipped terrain, it floats 0 / 2 / 28
 * / 45 buildings and by up to 9.77 m: the cap's INTERIOR is one tessellation of the ring
 * left inside the pavement rim, and the tessellator is free to run a triangle clean across
 * the block, so the height of a corner the piece never comes near appears under it. Nothing
 * local to the piece can see that. The only sound answer is to ask the surface, which is
 * what this class is for.
 *
 * **It is the drawn cap and not a model of it.** The triangles come out of
 * ExtrudePoly.BuildCap - the very call GenerateClusterQuartersOperator emits the floor
 * through - so the surface a building is founded on and the surface the player walks on are
 * one expression. That is §7r's and §7s's rule applied to the block floor: a second
 * derivation agrees with the drawn one exactly until one of the two is edited.
 *
 * **In GROUND terms.** The outline carries Quarter.CornerGroundHeightAt rather than
 * RoadSurface.HeightAtJunction, so every height here is a ground height and the drawn cap
 * is this surface plus MetaGen.ClusterStreetHeight plus MetaGen.QuarterSidewalkOffset,
 * vertex for vertex. Two reasons, and the second is the load bearing one: the storey index
 * is a difference of two GROUND heights with no constant in it, which is what makes it
 * exactly zero on a flat city rather than the ceiling of a rounding error; and adding a
 * constant and taking it away again is not the identity in single precision.
 *
 * The tessellation is decided by the plan and the plane only, so a constant height offset
 * cannot change which triangles come out - which is what makes "the drawn cap is this one
 * plus two constants" an equality rather than an approximation.
 */
public sealed class BlockFloor
{
    private static readonly Dc _dc = Dc.StreetGen;


    /**
     * The cap, as triangles, in the block's own cluster plan coordinates.
     */
    private readonly Vector3[] _a, _b, _c;
    private readonly Vector4[] _box;

    /**
     * Every distinct corner of the cap, on the decimetre grid the whole of this work stream
     * measures in - so that the rim's copy of a corner and the interior's copy of it are one
     * vertex, and the edge between the two is not a zero length one.
     */
    private readonly Vector3[] _verts;

    /**
     * Every distinct edge of the cap, as index pairs into _verts.
     */
    private readonly int[] _edgeFrom, _edgeTo;


    public int TriangleCount => _a.Length;


    /**
     * Every distinct corner of the cap, for a caller that wants to know what the surface is
     * made of rather than what it answers.
     */
    public IReadOnlyList<Vector3> Vertices => _verts;


    /**
     * The floor of one block, or null for a block that has no cap at all.
     *
     * Cached on the quarter, the way its pad is: a block's outline does not change once it
     * has been traced, and the alternative is one tessellation per building per fragment
     * operator that asks.
     */
    public static BlockFloor Of(Quarter quarter) => quarter?.GetBlockFloor();


    /**
     * The block's outline at its corners' own GROUND heights.
     *
     * The plan is the delimiters' corners and the height is that corner's own junction,
     * exactly as GenerateClusterQuartersOperator.FloorOutlineOf takes it - that one adds
     * the road offset and the junction's level on top, and the assertion that the two
     * differ by exactly those terms is BlockFloorTests' business.
     */
    public static List<Vector3> GroundOutlineOf(Quarter quarter)
    {
        var delims = quarter.GetDelims();
        var outline = new List<Vector3>(delims.Count);
        foreach (var delim in delims)
        {
            outline.Add(new Vector3(
                delim.StartPoint.X, quarter.CornerGroundHeightAt(delim), delim.StartPoint.Y));
        }

        return outline;
    }


    /**
     * Build the cap of one block, or null when there is none to build.
     */
    internal static BlockFloor Build(Quarter quarter)
    {
        if (null == quarter) return null;

        var outline = GroundOutlineOf(quarter);
        if (outline.Count < 3) return null;

        var inset = GenerateClusterQuartersOperator.PavementInsetOf(quarter, outline);

        var mesh = new joyce.Mesh("blockfloor");
        try
        {
            ExtrudePoly.BuildCap(mesh, outline, (List<CapInsetEdge>)inset, Vector3.UnitY, false);
        }
        catch (Exception e)
        {
            /*
             * A block whose cap cannot be tessellated has no floor for a building to stand
             * on either, and the caller falls back to the block's own corner range - which
             * is sound and merely loose. Reported rather than swallowed: it happens on no
             * block of the seventy shipped cities on either flag, so if it ever does the
             * buildings there are sunk by metres for a reason nothing else would name.
             */
            Warning(_dc,
                $"The block at {quarter.GetCenterPoint()} of cluster "
                + $"'{quarter.ClusterDesc?.Name}' has no floor to found a building on: {e}");

            return null;
        }

        return 0 == mesh.Indices.Count ? null : new BlockFloor(mesh);
    }


    private BlockFloor(joyce.Mesh mesh)
    {
        int n = mesh.Indices.Count / 3;
        _a = new Vector3[n];
        _b = new Vector3[n];
        _c = new Vector3[n];
        _box = new Vector4[n];

        var verts = new List<Vector3>();
        var index = new Dictionary<(int, int, int), int>();
        var edges = new HashSet<(int, int)>();

        int Intern(in Vector3 v)
        {
            /*
             * On the decimetre grid the whole of this work stream measures in, so that the
             * rim's copy of a corner and the interior's copy of it are one vertex and the
             * edge between them is not a zero length one.
             */
            var key = ((int)MathF.Round(v.X * 10f), (int)MathF.Round(v.Y * 10f),
                (int)MathF.Round(v.Z * 10f));
            if (index.TryGetValue(key, out int at)) return at;

            index[key] = verts.Count;
            verts.Add(v);
            return verts.Count - 1;
        }

        for (int i = 0; i < n; ++i)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[3 * i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[3 * i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[3 * i + 2]];

            _a[i] = a;
            _b[i] = b;
            _c[i] = c;
            _box[i] = new Vector4(
                Single.Min(a.X, Single.Min(b.X, c.X)), Single.Min(a.Z, Single.Min(b.Z, c.Z)),
                Single.Max(a.X, Single.Max(b.X, c.X)), Single.Max(a.Z, Single.Max(b.Z, c.Z)));

            int ia = Intern(a), ib = Intern(b), ic = Intern(c);
            foreach (var (u, v) in new[] { (ia, ib), (ib, ic), (ic, ia) })
            {
                if (u == v) continue;
                edges.Add(u < v ? (u, v) : (v, u));
            }
        }

        _verts = verts.ToArray();
        _edgeFrom = new int[edges.Count];
        _edgeTo = new int[edges.Count];

        int k = 0;
        foreach (var (u, v) in edges)
        {
            _edgeFrom[k] = u;
            _edgeTo[k] = v;
            ++k;
        }
    }


    /**
     * The floor's ground height at a plan position, from the triangle it falls in.
     */
    public bool TryHeightAt(in Vector2 v2Cluster, out float ground)
    {
        for (int i = 0; i < _a.Length; ++i)
        {
            var box = _box[i];
            if (v2Cluster.X < box.X - 0.05f || v2Cluster.X > box.Z + 0.05f
                || v2Cluster.Y < box.Y - 0.05f || v2Cluster.Y > box.W + 0.05f)
            {
                continue;
            }

            if (_tryBarycentric(_a[i], _b[i], _c[i], v2Cluster, out ground))
            {
                return true;
            }
        }

        ground = 0f;
        return false;
    }


    /**
     * The floor's lowest and highest ground height over a plan polygon, EXACTLY.
     *
     * The cap is affine on each of its triangles, so the extremes of the surface over a
     * region are attained at the corners of that region's intersection with one of them -
     * i.e. at a corner of the polygon, at a corner of the cap inside the polygon, or where
     * an edge of the cap crosses an edge of the polygon. All three are taken, which is why
     * this is the exact range and not a sample of it.
     *
     * Neither the polygon's corners alone nor the cap's boundary alone would do. The
     * corners alone miss a triangle that runs across the block and dips under the middle of
     * the piece, which is exactly the 9.77 m float measured before this class existed.
     *
     * ⚠️ THE CROSSINGS ARE NOT DECORATION. Measured, the lowest point of the floor over a
     * footprint is on the footprint's own boundary BETWEEN two of its corners more often
     * than not, and a grid over the interior misses it by up to 0.14 m. The middle term - a
     * cap corner strictly inside the polygon - decides nothing the game builds, and
     * BuildingFootingWorldTests says so with two refuted guesses at why; it is here because
     * the enumeration is only exact with it.
     *
     * @param footprint
     *     A closed ring in the block's own cluster coordinates; Y is ignored.
     * @returns false when the polygon does not meet the cap at all, for the caller to fall
     *     back to the block's corner range.
     */
    public bool TryBoundsOver(IList<Vector3> footprint, out float lo, out float hi)
    {
        float min = Single.MaxValue, max = Single.MinValue;
        lo = min;
        hi = max;

        if (null == footprint || footprint.Count < 3) return false;

        int m = footprint.Count;
        var plan = new Vector2[m];
        for (int i = 0; i < m; ++i) plan[i] = new Vector2(footprint[i].X, footprint[i].Z);

        float x0 = Single.MaxValue, y0 = Single.MaxValue;
        float x1 = Single.MinValue, y1 = Single.MinValue;
        foreach (var p in plan)
        {
            x0 = Single.Min(x0, p.X); x1 = Single.Max(x1, p.X);
            y0 = Single.Min(y0, p.Y); y1 = Single.Max(y1, p.Y);
        }

        void Take(float h)
        {
            min = Single.Min(min, h);
            max = Single.Max(max, h);
        }

        foreach (var p in plan)
        {
            if (TryHeightAt(p, out float h)) Take(h);
        }

        foreach (var v in _verts)
        {
            if (v.X < x0 || v.X > x1 || v.Z < y0 || v.Z > y1) continue;
            if (_contains(plan, new Vector2(v.X, v.Z))) Take(v.Y);
        }

        for (int e = 0; e < _edgeFrom.Length; ++e)
        {
            Vector3 p = _verts[_edgeFrom[e]], q = _verts[_edgeTo[e]];
            if (Single.Max(p.X, q.X) < x0 || Single.Min(p.X, q.X) > x1
                || Single.Max(p.Z, q.Z) < y0 || Single.Min(p.Z, q.Z) > y1)
            {
                continue;
            }

            Vector2 pp = new(p.X, p.Z), qq = new(q.X, q.Z);
            for (int i = 0; i < m; ++i)
            {
                if (_crossing(pp, qq, plan[i], plan[(i + 1) % m], out float t))
                {
                    Take(p.Y + t * (q.Y - p.Y));
                }
            }
        }

        lo = min;
        hi = max;

        return min <= max;
    }


    private static bool _tryBarycentric(
        in Vector3 a, in Vector3 b, in Vector3 c, in Vector2 p, out float h)
    {
        Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z), pc = new(c.X, c.Z);

        float d = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
        if (Single.Abs(d) < 1e-9f)
        {
            h = 0f;
            return false;
        }

        float l1 = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / d;
        float l2 = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / d;
        float l3 = 1f - l1 - l2;

        if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f)
        {
            h = 0f;
            return false;
        }

        /*
         * As an offset from one corner rather than as a weighted sum of three. The three
         * weights add to one only to within a rounding error, so l1*a + l2*b + l3*c over a
         * LEVEL triangle comes back a unit in the last place away from the level it is at -
         * and the storey index below is a difference of two ground heights that has to be
         * exactly zero on a flat city, not the ceiling of a rounding error.
         */
        h = c.Y + l1 * (a.Y - c.Y) + l2 * (b.Y - c.Y);
        return true;
    }


    private static bool _contains(in Vector2[] poly, in Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y)
                / (poly[j].Y - poly[i].Y) + poly[i].X)
            {
                inside = !inside;
            }
        }

        return inside;
    }


    /**
     * Where along ab the two plan segments cross, or false if they do not.
     */
    private static bool _crossing(
        in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d, out float t)
    {
        t = 0f;

        Vector2 r = b - a, s = d - c;
        float denom = r.X * s.Y - r.Y * s.X;
        if (Single.Abs(denom) < 1e-12f) return false;

        Vector2 ca = c - a;
        float tt = (ca.X * s.Y - ca.Y * s.X) / denom;
        float uu = (ca.X * r.Y - ca.Y * r.X) / denom;

        if (tt < 0f || tt > 1f || uu < 0f || uu > 1f) return false;

        t = tt;
        return true;
    }
}
