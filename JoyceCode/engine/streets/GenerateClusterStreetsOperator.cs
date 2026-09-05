using engine.world;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using engine.joyce;
using engine.joyce.components;
using engine.world.components;
using static engine.Logger;

namespace engine.streets;


internal class ClusterStreetsData
{
    public FragmentVisibility Visibility = new();
}


internal class Artefact
{
    public joyce.Mesh g;
 //   public joyce.Mesh ng;
}

/**
 * Create the 3d street geometry.
 */
public class GenerateClusterStreetsOperator : world.IFragmentOperator
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    static private object _lo = new();

    private ClusterDesc _clusterDesc;

    private string _myKey;
    private bool _traceStreets = false;

    public string FragmentOperatorGetPath()
    {
        return $"5001/GenerateClusterStreetsOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public static readonly float[] _uvTris =
    {
        0.25f, 0.5f, 0.251f, 0.51f, 0.250f, 0.51f
    };

    
    /**
     * Generate a polygon representing the street point.
     */
    /**
     * Emit the polygon capping one junction.
     *
     * Takes no Fragment: whether this junction belongs to the fragment being built is
     * the caller's decision, hoisted out so that the geometry itself can be produced -
     * and therefore compared - without booting an engine. See StreetGeometryTests.
     */
    internal bool _generateJunction(
        float cx, float cy,
        in streets.StreetPoint sp,
        Artefact a
    )
    {
        var g = a.g;
        //var ng = a.ng;

        /*
         * We simple generate a polygon using the section points as edges.
         * We triangulate it by creating a fan around the middle.
         */
        var secArray = sp.GetSectionArray();
        uint l = (uint)secArray.Count;
        if (l < 2)
        {
            // No need to generate a junction.
            return true;
        }

        /*
         * A junction is one node in the stroke graph, so it gets one height, and every
         * stroke that meets here, every deck and cap collider, and the kerb of every block
         * cornering here reads the same one - which is what keeps a non-planar network
         * consistent: the surfaces cannot disagree at the seam because there is only one
         * number. It is written once, in generation.RoadSurface, for that reason.
         */
        float h = generation.RoadSurface.HeightAtJunction(_clusterDesc.StreetHeightSource, sp);

        /*
         * First compute the center of the array, we need it for both
         * triangulation and for the uv values.
         */
        float ax = 0f;
        float ay = 0f;
        foreach (var p in secArray)
        {
            ax += p.X;
            ay += p.Y;
        }

        ax /= l;
        ay /= l;

        /*
         * Now create the vertices and the uv values.
         * We start with a center point.
         */
        {
            uint i0 = g.GetNextVertexIndex();
            g.p(ax + cx, h, ay + cy); g.N(Vector3.UnitY);
            
            g.UV(_uvTris[0],_uvTris[1]);
            int uvIndex = 0;
            foreach (var b in secArray)
            {
                g.p(b.X + cx, h, b.Y + cy); g.N(Vector3.UnitY);
                g.UV(_uvTris[2+uvIndex],_uvTris[2+uvIndex+1]);
                uvIndex = (uvIndex + 2) & 3;
            }

            /*
             * Now create the vertex indices for the triangles.
             */
            for (uint k = 0; k < l; ++k)
            {
                uint knext = (k + 1) % l;
                g.Idx(i0 + 0, i0 + 1 + knext, i0 + 1 + k);
            }
        }

        /*
         * That's it!
         */
        return true;
    }


    private bool _checkUV(in Vector2 uv)
    {
        if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f)
        {
            Trace(_dc, $"uv out of range: {uv}.");
            return false;
        }

        return true;
    }

    private bool _checkTriUV(in Vector2 uva, in Vector2 uvb, in Vector2 uvc)
    {
        bool result = true;
        result = result && _checkUV(uva);
        result = result && _checkUV(uvb);
        result = result && _checkUV(uvc);
        if ( (uvb-uva).LengthSquared()<0.00001f )
        {
            Trace(_dc, $"uvb {uvb} too close to uva {uva}.");
            result = false;
        }
        if ( (uvc-uvb).LengthSquared()<0.000001f )
        {
            Trace(_dc, $"uvc {uvc} too close to uvb {uvb}.");
            result = false;
        }
        if ( (uva-uvc).LengthSquared()<0.000001f )
        {
            Trace(_dc, $"uva {uva} too close to uvc {uvc}.");
            result = false;
        }

        return result;
    }


    /**
     * The most rows one texture length of carriageway may be cut into.
     *
     * A bound on the arithmetic rather than on the geometry: the number asked for is a
     * texture length times the difference of the two sides' slopes over four times
     * generation.RoadSurface.MaxSag, and a stroke whose two section points nearly coincide
     * has a slope that is nearly unbounded. Over the five baseline cities on the shipped
     * terrain the largest number actually asked for is 51, so this does not bind on
     * anything the generator produces - and a city where it DID bind would be one whose road
     * is silently coarser than MaxSag rather than one that is merely expensive, which is
     * what RoadTessellationTests.TheDrawnRoadStaysOnItsOwnSurface would then catch.
     */
    internal const int MaxRowsPerTextureLength = 64;


    /**
     * One row of carriageway vertices: the two points where the road meets its two kerbs
     * at a given distance along the stroke, built flat at the A end's height for
     * _shearOntoSlope to lift afterwards.
     *
     * Here rather than written out twice inside the row loop because the loop now emits a
     * variable number of rows per texture length, and the two copies it used to have - one
     * for the row it started at and one for the row it ended at - were the same six lines
     * with different variable names.
     */
    private void _streetRow(
        joyce.Mesh g, in builtin.tools.UVProjector uvp,
        in Vector3 vam, in Vector2 q, in Vector2 n, float hsw, float h,
        float d, float vStart)
    {
        /*
         * Direction of street, scaled by the current offset, plus street point A in
         * fragment coordinates at the standard height.
         */
        var em = new Vector3(q.X, 0f, q.Y);
        em *= d;
        em += vam;

        var elx = em.X - hsw * n.X;
        var ely = em.Z - hsw * n.Y;
        var erx = em.X + hsw * n.X;
        var ery = em.Z + hsw * n.Y;
        var uv0 = uvp.GetUV(new Vector3(elx, h, ely), 0f, vStart);
        var uv1 = uvp.GetUV(new Vector3(erx, h, ery), 0f, vStart);

        if (_traceStreets)
            Trace(_dc,
                $"row @{d}: el = ({elx}; {ely}); uv = ({uv0.X}; {uv0.Y}); "
                + $"er = ({erx}; {ery}); uv = ({uv1.X}; {uv1.Y})");

        g.p(elx, h, ely); g.N(Vector3.UnitY);
        g.UV(uv0.X, uv0.Y);
        g.p(erx, h, ery); g.N(Vector3.UnitY);
        g.UV(uv1.X, uv1.Y);
    }


    private void _streetTriangle(in builtin.tools.UVProjector uvp, float vStart, in Vector3 vA, in Vector3 vB,
        in Vector3 vC, in Artefact a)
    {
        var g = a.g;

        /*
         * Emit triangle at a, run will start at height of dar.
         * Tri: al, al @ height of dar (cl), ar
         *
         * Note that we start from the beginning in the texture.
         */
        uint i0 = g.GetNextVertexIndex();
        var uvA = uvp.ProjectUV(vA, 0f, vStart);
        var uvB = uvp.ProjectUV(vB, 0f, vStart);
        var uvC = uvp.ProjectUV(vC, 0f, vStart);

        /*
         * Now all UVs are in the [0...1] space coordinate.
         * U would be in range by program design, but V will
         * probably wrap. So align everything on V = 0, scale
         * down if larger.
         */
        var vMin = Single.Min(Single.Min(uvA.Y, uvB.Y), uvC.Y);
        var vMax = Single.Max(Single.Max(uvA.Y, uvB.Y), uvC.Y);
        var vSize = vMax - vMin;

        if (vMin > 0f && vMax <= 1f)
        {
        }
        else
        {
            /*
             * Simple algorith, just clamp everything to 0f,
             */
            var uvOffset = new Vector2(0f, vMin);

            Vector2 uvScale;
            if (vSize > 1f)
            {
                uvScale = new Vector2(1f, 1f / vSize);
            }
            else
            {
                uvScale = Vector2.One;
            }

            uvA = (uvA - uvOffset) * uvScale;
            uvB = (uvB - uvOffset) * uvScale;
            uvC = (uvC - uvOffset) * uvScale;
        }

        uvA = uvp.ScalePixelUV(uvA);
        uvB = uvp.ScalePixelUV(uvB);
        uvC = uvp.ScalePixelUV(uvC);

        // Debug.WriteIf(!_checkTriUV(uvA, uvB, uvC), "Triangle UV problem");
        g.p(vA); g.N(Vector3.UnitY); g.UV(uvA);
        g.p(vB); g.N(Vector3.UnitY); g.UV(uvB);
        g.p(vC); g.N(Vector3.UnitY); g.UV(uvC);
        g.Idx(i0 + 0, i0 + 1, i0 + 2);
    }


    /**
      * Generate the streets between any junctions.
      */
    /**
     * Emit the road surface of one stroke. Fragment-free for the same reason as
     * _generateJunction above.
     */
    internal bool _generateStreetRun(
        float cx, float cy,
        streets.Stroke stroke,
        Artefact a)
    {
        var g = a.g;
        
        /*
         * We need the material to know the texture size in use.
         */
        var jMat = I.Get<ObjectRegistry<Material>>().Get("engine.streets.materials.street");
        var jStreetTexture = jMat.Texture;
        
        /*
         * We need the intersection points for this stroke in each of its street points
         * to have the polygon that makes up the road.
         *
         * If this stroke is the only one at any street endpoint, we generate the
         * outer points from the street point and the width of the road.
         *
         * We need to compute aleft, aright, bleft and bright, and we use these
         * names from the perspective from a to b.
         */
        float sw = stroke.StreetWidth();
        float hsw = sw / 2f;
        Vector2 n = stroke.Normal;
        Vector3 n3 = new(n.X, 0f, n.Y);
        Vector2 q = stroke.Unit;
        Vector3 q3 = new(q.X, 0f, q.Y);
        /*
         * The whole surface is built from v3Cluster plus planar offsets, so building it
         * at the A end's height puts every vertex of a flat stroke where it belongs.
         *
         * The two ends differ when the stroke is a ramp between decks, and equally when
         * it simply runs downhill - _shearOntoSlope has never known or cared which.
         * Rather than threading two heights through the fifteen emission sites below,
         * the surface is built flat at hA and then sheared onto the slope afterwards.
         * That works because the UV projector's axes are both planar, so a vertex's Y
         * cannot affect its UV - which means moving Y after the fact disturbs nothing
         * else.
         */
        var heightSource = _clusterDesc.StreetHeightSource;
        float hA = generation.RoadSurface.HeightAtJunction(heightSource, stroke.A);
        float hB = generation.RoadSurface.HeightAtJunction(heightSource, stroke.B);

        var h = hA;
        Vector3 v3Cluster = new(cx, h, cy);

        /*
         * Everything this stroke emits lands at or after this index.
         */
        uint firstVertex = g.GetNextVertexIndex();
        

        var spA = stroke.A;
        var spB = stroke.B;

        /*
         * The exterior points of the street area.
         */
        Vector3 al, ar, bl, br;

        /*
         * The linear logical part of the street.
         */
        Vector3 am, bm;

        am = v3Cluster + new Vector3(spA.Pos.X, 0f, spA.Pos.Y);
        bm = v3Cluster + new Vector3(spB.Pos.X, 0f, spB.Pos.Y);
        if (_traceStreets) Trace(_dc, $"am = ({am}); bm = ({bm});");

        /*
         * Where this carriageway begins and ends, read from the section arrays of the two
         * junctions - hoisted into generation.RoadSurface so that the satnav guideline can
         * be drawn on the SAME four corners rather than on a second derivation of them.
         * See RoadSurface.TryCornersOf, which is these thirty lines and nothing else.
         */
        if (!generation.RoadSurface.TryCornersOf(
                stroke, out var v2al, out var v2ar, out var v2bl, out var v2br, out var why))
        {
            ErrorThrow(why, le => new InvalidOperationException(le));
        }

        al = v3Cluster + new Vector3(v2al.X, 0f, v2al.Y);
        ar = v3Cluster + new Vector3(v2ar.X, 0f, v2ar.Y);
        bl = v3Cluster + new Vector3(v2bl.X, 0f, v2bl.Y);
        br = v3Cluster + new Vector3(v2br.X, 0f, v2br.Y);

        /*
         * The surface this stroke is about to be built flat on, and then sheared onto.
         *
         * Built here, from the four section points the mesh itself uses as its corners, so
         * that the shear cannot be describing a different road from the one being emitted.
         * al/bl and ar/br are the pairs a block edge runs between - see RoadSurface.
         */
        var roadSurface = generation.RoadSurface.Of(
            new Vector2(am.X, am.Z), q,
            new Vector2(al.X, al.Z), new Vector2(ar.X, ar.Z),
            new Vector2(bl.X, bl.Z), new Vector2(br.X, br.Z),
            hA, hB);

        // TXWTODO: Factor out the code to triangulate and texture the street part.

        /*
         * Different triangulation approach for every street [section]:
         * - We know a linear path the street shall be built along, defined by am-bm
         * - we can project al, ar, bl and br on the line (dot product).
         * - the "remaining space" between a and b (rectangle parallel to am-bm) can be
         *   filled with one standard triangle.
         * - the outer parts can be built of two triangles.
         *      which (TXWTODO) we force to fit into one texture.
         */
        var vam = am;
        var vambm = bm - am;

        /*
         * Emit tris for am-bm.
         *
         * Street layout:
         * The texture width is applied to the whole street width.
         * Therefore, the street texture lasts 4 times its width.
         */
        var texlen = stroke.StreetWidth() * 4f;

        /*
         * This defines which part of the street texture we are about to use.
         */
        Vector2 uvStreetOrigin = new Vector2(0.5f, 0f);
        Vector2 uvStreetSize = new(0.25f, 1f);
        
        /*
         * So we initialize our uv projector with uv origin at am - half street width.
         * (left side of street at am).
         */
        var uvp = new builtin.tools.UVProjector(
            new Vector3(vam.X - n.X * hsw, h, vam.Z - n.Y * hsw),
            new Vector3(n.X * sw, 0f, n.Y * sw), // That is the logical size of the u [0..1[ interval.
            new Vector3(q.X * texlen, 0f, q.Y * texlen),
            uvStreetOrigin,
            uvStreetSize,
            jStreetTexture.Size2);

        /*
         * These are the 4 edge points of the street, projected to street,
         * unit is meters.
         */
        float dal = Vector3.Dot(al-am, vambm) / vambm.Length();
        float dar = Vector3.Dot(ar-am, vambm) / vambm.Length();
        float dbl = Vector3.Dot(bl-am, vambm) / vambm.Length();
        float dbr = Vector3.Dot(br-am, vambm) / vambm.Length();

        float dStart = 0f;
        float vStart = 0f;
        float damax, damin;
        
        if (dal < dar)
        {
            damax = dar;
            damin = dal;
        }
        else
        {
            damax = dal;
            damin = dar;
        }

        float dbmin, dbmax; 
        
        if (dbl < dbr)
        {
            dbmin = dbl;
            dbmax = dbr;
        }
        else
        {
            dbmin = dbr;
            dbmax = dbl;
        }

        if (_traceStreets) Trace(_dc, $"d[ab][min/max]: {damin}; {damax}; {dbmin}; {dbmax};");

        /*
         * Handle special case of a and b ends overlapping
         */
        if (damax > dbmin)
        {
            /*
             * Now create the vertices and the uv values.
             * We start with a center point.
             */
            if (true) {
                uint i0 = g.GetNextVertexIndex();
                g.p(al); g.N(Vector3.UnitY);
                g.UV(0.125f, 0.25f);
                g.p(ar); g.N(Vector3.UnitY);
                g.UV(0.128f, 0.25f);
                g.p(bl); g.N(Vector3.UnitY);
                g.UV(0.125f, 0.26f);
                g.p(br); g.N(Vector3.UnitY);
                g.UV(0.128f, 0.26f);
                
                g.Idx(i0 + 0, i0 + 2, i0 + 1);
                g.Idx(i0 + 1, i0 + 2, i0 + 3);
            }

            /*
             * Sheared like any other stroke, and this used to be the one path that was not.
             * The quad's four vertices ARE the four section points, so each lands on its own
             * junction's height and the little filler between two overlapping junction
             * footprints joins both caps and both kerbs instead of lying flat at the A end's
             * height. On the flat city the shear is a no-op, as everywhere else.
             */
            _shearOntoSlope(g, firstVertex, roadSurface);

            /*
             * Which is why we do not need to render a road at all.
             */
            return true;
        }


        /*
         * This is the triangles at the A point
         *
         * Thje c points are the point at the side of the outermost a/b point
         * at the height of the innermost one.
         *
         * Note the triangles appeaer clockwise in the xy plane, later, in the
         * xz plane, they will be ccw.
         */
        if (dal < dar)
        {
            var cl = vam + q3 * dar - n3 * hsw;
            _streetTriangle(uvp, vStart,al, cl, ar, a);
        }
        else
        {
            var cr = vam + q3 * dal + n3 * hsw;
            _streetTriangle(uvp, vStart,ar, al, cr, a);
        }
        
        if (dbl < dbr)
        {
            var cr = vam + q3 * dbl + n3 * hsw;
            _streetTriangle(uvp, vStart,bl, br, cr, a);
        }
        else
        {
            var cl = vam + q3 * dbr - n3 * hsw;
            _streetTriangle(uvp, vStart,bl, br, cl, a);
        }

        /*
         * Starting from am, we layout the street in rows.
         * Emit vertex rows until we are at dbmin.
         */
        {
            if (_traceStreets) Trace(_dc, $"New rect list.");

            /*
             * How long a row may be before its two triangles depart from the surface they
             * are cut from by more than generation.RoadSurface.MaxSag.
             *
             * Infinite for a level stroke and for a straight one - both sides climb at the
             * same rate then - so a flat city and every ramp emit exactly the rows they
             * always did, at exactly the same floats. See RoadSurface.MaxRowSpan.
             */
            float maxRowSpan = roadSurface.MaxRowSpan;

            /*
             * We start at damax.
             */
            var currD = damax;
            var finalD = dbmin;

            dStart = 0f;
            vStart = 0f;
            /*
             * Or the other way round?
             */
            while ((currD - dStart) < 0f)
            {
                dStart -= texlen;
                vStart -= 1f;
            }

            while ((currD - vStart) > texlen)
            {
                dStart += texlen;
                vStart += 1f;
            }

            bool isFirstSegment = true;
            while (true)
            {
                /*
                 * Compute nextD.
                 *
                 * nextD is the minimum of
                 *  - the next multiple of texlen
                 *  - finalD
                 */
                float nextD;
                {
                    float nextWholeD = (float)Math.Ceiling(currD / texlen) * texlen;
                    if ((nextWholeD - currD) < 0.001f)
                    {
                        nextWholeD = nextWholeD + texlen;
                    }

                    nextD = Math.Min(nextWholeD, finalD);
                }

                /*
                 * How many rows this one texture length is cut into. One - i.e. exactly the
                 * geometry that was emitted before - unless the two sides of the road climb
                 * at different rates, which is what makes the surface between the two kerbs
                 * a twisted one that two triangles cannot represent.
                 */
                int nSub = 1;
                if (Single.IsFinite(maxRowSpan) && maxRowSpan > 0f)
                {
                    nSub = Math.Clamp(
                        (int)Single.Ceiling((nextD - currD) / maxRowSpan),
                        1, MaxRowsPerTextureLength);
                }

                /*
                 * The extra rows are INSIDE one texture length, so they all take the same
                 * vStart and the texture runs across them exactly as it ran across the
                 * single long row: uvp.GetUV computes v from the position's own distance
                 * along the stroke, and vStart only says which repetition of the texture
                 * this row belongs to.
                 */
                uint iRow = g.GetNextVertexIndex();
                _streetRow(g, uvp, vam, q, n, hsw, h, currD, vStart);

                /*
                * If this is the first segment, also emit navmesh
                */
                if (isFirstSegment)
                {
                    //ng.p(elx, h, ely); ng.N(Vector3.UnitY);
                    //ng.UV(uv0.X, uv0.Y);
                    //ng.p(erx, h, ery); ng.N(Vector3.UnitY);
                    //ng.UV(uv1.X, uv1.Y);
                    isFirstSegment = false;
                }

                for (int sub = 1; sub <= nSub; ++sub)
                {
                    /*
                     * The last one is nextD itself rather than a fraction of the way to
                     * it, so that an undivided row is the same float it always was.
                     */
                    float subD = sub == nSub
                        ? nextD
                        : currD + (nextD - currD) * ((float)sub / (float)nSub);

                    _streetRow(g, uvp, vam, q, n, hsw, h, subD, vStart);

                    uint i0 = iRow + (uint)((sub - 1) * 2);
                    g.Idx(i0 + 1, i0 + 0, i0 + 2);
                    g.Idx(i0 + 1, i0 + 2, i0 + 3);
                }

                vStart += 1;

                /*
                 * Finish, if we already reached finalD.
                 */
                if (nextD == finalD)
                {
                    //ng.p(flx, h, fly); ng.N(Vector3.UnitY);
                    //ng.UV(uv2.X, uv2.Y);
                    //ng.p(frx, h, fry); ng.N(Vector3.UnitY);
                    //ng.UV(uv3.X, uv3.Y);

                    //ng.Idx(ni0 + 1, ni0 + 0, ni0 + 2);
                    //ng.Idx(ni0 + 1, ni0 + 2, ni0 + 3);
                    break;
                }


                // TODO: Small adjustment: If nextD is too close to finalD but not equal, set it to finalD.

                currD = nextD;
            }
        }

        _shearOntoSlope(g, firstVertex, roadSurface);

        return true;
    }


    /**
     * Tilt a stroke's surface from flat onto its slope.
     *
     * Everything above builds the surface at the A end's height, which is already right
     * for a flat stroke and is why this is a no-op for every street on the ground. A
     * stroke whose ends differ - a ramp between decks, or equally a street running
     * downhill - then needs its vertices lifted, and a normal that is no longer straight
     * up, or a climbing surface lights as though it were flat.
     *
     * Done as a pass over the vertices this stroke emitted rather than at each emission
     * site: there are about fifteen of them, and the Y of a vertex affects nothing else
     * here - the UV projector's two axes are both planar, so UVs are unchanged by it.
     *
     * **Each SIDE of the road climbs between its own two section points**, which is the
     * whole subtlety of this method, and it is a property of the seams rather than of the
     * road. A junction cap is a flat fan at that junction's one height, and a block's kerb
     * is a straight chord between two section points each at its own junction's height, so
     * the surface has to reach BOTH exactly: the two corner vertices at each end at that
     * end's height, and everything along a kerb line on the straight segment between them.
     * Interpolating each side between its own pair delivers both at once, because the axial
     * coordinate is affine along a chord.
     *
     * Heighting every vertex by its axial projection over the whole plan length delivers
     * neither, and that was a real tear rather than a rounding error: at a 15 degree bend
     * the two corners of one junction project to 0.858 and 1.142 of the stroke length, so
     * the two strokes meeting there disagreed by up to 1.8 m on an 8 % grade and the road
     * split open at the junction. Holding the surface flat over ONE window along the centre
     * line - up to the further of the two A corners and from the nearer of the two B corners
     * - fixes the junctions and leaves the kerbs: the kerb chord and that three part profile
     * agree at both ends and nowhere in between, by up to 6.5 m on the shipped terrain. See
     * generation.RoadSurface for the measurement.
     *
     * At a STRAIGHT junction both section points are at the same axial distance, so the two
     * sides share one window and this is exactly what the single window emitted. Every ramp
     * OverpassBuilder makes is straight, which is why ramps are unchanged float for float.
     *
     * @param surface
     *     The four section points bounding this stroke, with its two junction heights -
     *     built by the caller from the very corners it emitted.
     */
    private void _shearOntoSlope(
        joyce.Mesh g, uint firstVertex, in generation.RoadSurface surface)
    {
        if (surface.IsLevel)
        {
            return;
        }

        /*
         * The normal is per side rather than per stroke, because the two sides of a bend do
         * not climb over the same run and so are not at the same angle. Straight up rotated
         * back by that slope, in the vertical plane the stroke runs along; a climbing
         * surface with a straight-up normal lights as though it were flat.
         *
         * Applied to every vertex including the wedges filling the junctions, deliberately:
         * shading stays continuous across the road rather than creasing at the junction
         * line, and the cap it abuts is a separate surface with its own normals either way.
         */
        for (int i = (int)firstVertex; i < g.Vertices.Count; ++i)
        {
            Vector3 v = g.Vertices[i];
            Vector2 p = new(v.X, v.Z);

            g.Vertices[i] = v with { Y = surface.HeightAt(p) };

            if (null != g.Normals && i < g.Normals.Count)
            {
                g.Normals[i] = surface.NormalAt(p);
            }
        }
    }


    public void _applyAnyVisibility(Fragment worldFragment)
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        if (_traceStreets) Trace(_dc, $"Obtaining streets.");
        var strokeStore = _clusterDesc.StrokeStore();
        if (_traceStreets) Trace(_dc, $"Have streets.");

        if (_traceStreets)
        {
            Trace(_dc, $"In terrain '{worldFragment.GetId()}' operator. "
                  + $"Fragment @{worldFragment.Position}. "
                  + $"Cluster '{_clusterDesc.IdString}' @{cx}, {cz}, R:{_clusterDesc.Size}.");
        }

        /*
         * We need the coordinates of the cluster relative to the fragment to translate
         * everything to fragment coorddinates.
         */

        var nGeneratedStreets = 0;
        var nIgnoredStrokes = 0;

        Artefact artefact = new()
        {
            g = engine.joyce.Mesh.CreateNormalsListInstance($"{worldFragment.GetId()}-streetsgenerator-streets"),
            //ng = engine.joyce.Mesh.CreateNormalsListInstance($"{worldFragment.GetId()}-streetsgenerator-navmesh"),
        };


        /*
         * Create the roads between the junctions.
         */
        foreach (var stroke in strokeStore.GetStrokes())
        {
            /*
             * By convention we only build streets whose A point is inside this
             * fragment, so that a stroke spanning two fragments is built exactly once.
             */
            if (!worldFragment.IsInsideLocal(stroke.A.Pos.X + cx, stroke.A.Pos.Y + cz))
            {
                ++nIgnoredStrokes;
                continue;
            }

            /*
             * These two counters were the wrong way round: a successful run was
             * counted as ignored. They feed a trace line only.
             */
            if (_generateStreetRun(cx, cz, stroke, artefact))
            {
                ++nGeneratedStreets;
            }
            else
            {
                ++nIgnoredStrokes;
            }
        }

        /*
         * Create the junctions.
         */
        if (true)
        {
            foreach (var streetPoint in strokeStore.GetStreetPoints())
            {
                if (!worldFragment.IsInsideLocal(streetPoint.Pos.X + cx, streetPoint.Pos.Y + cz))
                {
                    continue;
                }

                _generateJunction(
                    cx, cz, streetPoint, artefact
                );
            }
        }

        if (_traceStreets) Trace(_dc, $"Created {nGeneratedStreets} strokes, discarded {nIgnoredStrokes}.");

        if (artefact.g.IsEmpty())
        {
            if (_traceStreets) Trace(_dc, $"Nothing to add at all.");
            return;
        }

        var e = worldFragment.Engine;
        
        #if false
        /*
         * Add the navmesh component: It just consists of a list of meshes, which we already have generated.
         * However we do not associate it with a particular fragment, so it won't get wiped out by the
         * fragment unload process. Instead, it becomes tagged with the cluster.
         *
         * Note, that this will lock the navmesh in-memory.
         *
         * Navmesh building will query this.
         */
        if (!artefact.ng.IsEmpty())
        {
            artefact.ng.Move(worldFragment.Position);
            e.QueueEntitySetupAction("GenerateClusterStreetsOperator.NavMesh", (entity) =>
            {
                entity.Set(new ClusterId
                {
                    Id = _clusterDesc.Id
                });
                entity.Set(new NavMesh
                {
                    ToWorld = Matrix4x4.CreateTranslation(worldFragment.Position),
                    Meshes = new List<Mesh>() { artefact.ng }
                });
                entity.Set(new FragmentId(worldFragment.NumericalId));
            });
        }
        #endif

        var matmesh = new MatMesh(
            I.Get<ObjectRegistry<Material>>().Get("engine.streets.materials.street"), 
            artefact.g);
        
        /*
         * We use an incredibly large distance due to the map camera.
         */
        engine.joyce.InstanceDesc instanceDesc = InstanceDesc.CreateFromMatMesh(matmesh, 100000f);
        
        /*
         * Now create a flat street level box as street physics.
         */

        List<Func<IList<StaticHandle>, Action>> listCreatePhysics = new();

        /*
         * Two mutually exclusive ways to give a city something to drive on.
         *
         * A flat city gets one floor plane per fragment: cheap, complete, and exactly
         * right because every street really is at that height. A city that follows its
         * terrain cannot have one - there is no height to put it at, and a plane through
         * the middle of the hills would be an invisible wall in every valley - so each
         * street carries its own surface instead.
         *
         * Emitting both would be worse than either: the plane would still cut through
         * the roads it was supposed to replace.
         */
        bool groundIsFlat = _clusterDesc.StreetHeightSource.IsFlat;

        if (groundIsFlat)
        {
            listCreatePhysics.Add((IList<StaticHandle> staticHandles) =>
            {
                lock (worldFragment.Engine.Simulation)
                {
                    float floorHeight = 0.1f;
                    Vector3 v3BodyOffset = new(0f, floorHeight / 2f, 0f);

                    // TXWTODO: We create the full fragment, now only the part containing the city
                    Vector3 v3BoxPos = worldFragment.Position with
                    {
                        Y = _clusterDesc.AverageHeight + world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE
                    };

                    var shape = new TypedIndex()
                    {
                        Packed = (uint)engine.physics.actions.CreateBoxShape.Execute(
                            worldFragment.Engine.PLog,
                            worldFragment.Engine.Simulation,
                            world.MetaGen.FragmentSize,
                            floorHeight,
                            world.MetaGen.FragmentSize,
                            out var pbody
                        )
                    };

                    StaticHandle staticHandle = worldFragment.Engine.Simulation.Statics.Add(
                        new StaticDescription(
                            v3BoxPos - v3BodyOffset,
                            Quaternion.Identity,
                            shape
                        ));

                    return () =>
                    {
                        lock (worldFragment.Engine.Simulation)
                        {
                            worldFragment.Engine.Simulation.Statics.Remove(staticHandle);
                        }
                    };
                }
            });
        }

        /*
         * A tilted box under each road surface the fragment floor does not cover: every
         * deck and ramp, and every street at all once the city follows its terrain.
         * Without one a vehicle drives through the bridge instead of over it, or through
         * the hillside road and out the other end.
         *
         * The junction caps between them get a slab each, below - see JunctionCollider.
         */
        var colliderHeights = _clusterDesc.StreetHeightSource;

        foreach (var stroke in strokeStore.GetStrokes())
        {
            if (!generation.DeckCollider.IsNeededFor(stroke, groundIsFlat))
            {
                continue;
            }

            /*
             * The same convention the surfaces are built under: a stroke belongs to the
             * fragment holding its A point, so a stroke spanning two fragments gets
             * exactly one collider. Without this every fragment overlapping the cluster
             * would emit a collider for every stroke in the whole city - harmless while
             * only bridges had them and nothing had any, and a pile of duplicate statics
             * the moment ordinary streets need them too.
             */
            if (!worldFragment.IsInsideLocal(stroke.A.Pos.X + cx, stroke.A.Pos.Y + cz))
            {
                continue;
            }

            Vector3 worldA = new Vector3(stroke.A.Pos.X + cx, 0f, stroke.A.Pos.Y + cz)
                             + worldFragment.Position
                             with
                             {
                                 Y = generation.RoadSurface.HeightAtJunction(
                                     colliderHeights, stroke.A)
                             };
            Vector3 worldB = new Vector3(stroke.B.Pos.X + cx, 0f, stroke.B.Pos.Y + cz)
                             + worldFragment.Position
                             with
                             {
                                 Y = generation.RoadSurface.HeightAtJunction(
                                     colliderHeights, stroke.B)
                             };

            var collider = generation.DeckCollider.For(
                worldA, worldB, stroke.StreetWidth(), 0.1f);

            if (collider.Length < 0.001f)
            {
                continue;
            }

            listCreatePhysics.Add((IList<StaticHandle> staticHandles) =>
            {
                lock (worldFragment.Engine.Simulation)
                {
                    var deckShape = new TypedIndex()
                    {
                        Packed = (uint)engine.physics.actions.CreateBoxShape.Execute(
                            worldFragment.Engine.PLog,
                            worldFragment.Engine.Simulation,
                            collider.Length,
                            collider.Thickness,
                            collider.Width,
                            out var pDeckBody
                        )
                    };

                    StaticHandle deckHandle = worldFragment.Engine.Simulation.Statics.Add(
                        new StaticDescription(collider.Position, collider.Orientation, deckShape));

                    return () =>
                    {
                        lock (worldFragment.Engine.Simulation)
                        {
                            worldFragment.Engine.Simulation.Statics.Remove(deckHandle);
                        }
                    };
                }
            });
        }

        /*
         * And a slab under each junction cap, which is the area BETWEEN the branches of
         * a junction: every stroke's box reaches the junction centre, so between two
         * neighbouring branches there is a wedge with nothing built under it at all. Over
         * that wedge the hover probe's ray misses everything, the ship is aimed at the
         * terrain instead of at the road, and it catches on the edges of the converging
         * stroke boxes.
         *
         * Emitted under the same conditions and the same ownership rule as the stroke
         * colliders above, so a flat city - where the fragment floor plane already covers
         * every junction on the ground - is untouched.
         */
        Vector3 v3ClusterOrigin = new Vector3(cx, 0f, cz) + worldFragment.Position;

        foreach (var streetPoint in strokeStore.GetStreetPoints())
        {
            if (!generation.JunctionCollider.IsNeededFor(streetPoint, groundIsFlat))
            {
                continue;
            }

            /*
             * A junction belongs to the fragment holding it, exactly as the junction
             * MESH loop above decides. Without this every fragment overlapping the
             * cluster would emit a slab for every junction in the whole city.
             */
            if (!worldFragment.IsInsideLocal(streetPoint.Pos.X + cx, streetPoint.Pos.Y + cz))
            {
                continue;
            }

            var cap = generation.JunctionCollider.For(
                streetPoint.GetSectionArray(),
                v3ClusterOrigin,
                generation.RoadSurface.HeightAtJunction(colliderHeights, streetPoint),
                0.1f);

            if (!cap.IsUsable)
            {
                continue;
            }

            listCreatePhysics.Add((IList<StaticHandle> staticHandles) =>
            {
                lock (worldFragment.Engine.Simulation)
                {
                    var simulation = worldFragment.Engine.Simulation;
                    var bufferPool = simulation.BufferPool;

                    int nPoints = cap.Points.Count;
                    bufferPool.Take<Vector3>(nPoints, out var hullPoints);
                    for (int i = 0; i < nPoints; ++i)
                    {
                        hullPoints[i] = cap.Points[i];
                    }

                    /*
                     * The hull comes back centred on its own centroid, so the static
                     * carries that centroid as its position - the same contract
                     * ExtrudePoly.BuildStaticPhys builds its compounds under.
                     */
                    bool created = ConvexHullHelper.CreateShape(
                        hullPoints.Slice(nPoints), bufferPool, out var v3HullCentre, out var hull);
                    bufferPool.Return(ref hullPoints);

                    if (!created)
                    {
                        return () => { };
                    }

                    var hullShape = simulation.Shapes.Add(hull);

                    StaticHandle capHandle = simulation.Statics.Add(
                        new StaticDescription(v3HullCentre, Quaternion.Identity, hullShape));

                    return () =>
                    {
                        lock (worldFragment.Engine.Simulation)
                        {
                            worldFragment.Engine.Simulation.Statics.Remove(capHandle);
                            hull.Dispose(bufferPool);
                        }
                    };
                }
            });
        }

        /*
         * Add the entity containing the instanceDesc.
         */
        worldFragment.AddStaticInstance(
            0x00800001, 
            "engine.streets.streets", 
            instanceDesc,
            listCreatePhysics);
    }

    
    /**
     * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
     */
    public Func<Task> FragmentOperatorApply(world.Fragment worldFragment, FragmentVisibility visib) => new (async () =>
    {
        var csd = worldFragment.FindOperatorData<ClusterStreetsData>(FragmentOperatorGetPath()); 

        /*
         * Special case for this operator: We only generate once for 3d and 2d, not separately
         */
        if ((csd.Visibility.How & FragmentVisibility.VisibleAny) != 0)
        {
            return;
        }

        csd.Visibility.How |= (byte)(FragmentVisibility.Visible3dNow | FragmentVisibility.Visible2dNow);
        
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            {
                float csh = _clusterDesc.Size / 2.0f;
                float fsh = world.MetaGen.FragmentSize / 2.0f;
                if (
                    (cx - csh) > (fsh)
                    || (cx + csh) < (-fsh)
                    || (cz - csh) > (fsh)
                    || (cz + csh) < (-fsh)
                )
                {
                    if (_traceStreets) Trace(_dc, $"Too far away: x={_clusterDesc.Pos.X}, z={_clusterDesc.Pos.Z}");
                    return;
                }
            }
        }

        if (_traceStreets) Trace(_dc, $"cluster '{_clusterDesc.Name}' ({_clusterDesc.IdString}) in range");


        /*
         * We just have one code that does it all.
         */
        _applyAnyVisibility(worldFragment);
        
    });


    public GenerateClusterStreetsOperator(
        in ClusterDesc clusterDesc,
        in string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;

        I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.streets.materials.street",
            (name) => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("streets1to4.png" /*, 
                    t => t.FilteringMode = Texture.FilteringModes.Framebuffer */)
            });
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateClusterStreetsOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}

