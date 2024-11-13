using engine.world;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
    private bool _generateJunction(
        in world.Fragment worldFragment,
        float cx, float cy,
        in streets.StreetPoint sp,
        Artefact a
    )
    {
        var g = a.g;
        //var ng = a.ng;
        
        /*
         * We render only street points inside our fragment.
         */
        if (!worldFragment.IsInsideLocal(sp.Pos.X + cx, sp.Pos.Y + cy))
        {
            return false;
        }

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

        float h = _clusterDesc.AverageHeight + 2.0f;

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
        float uFactor = (1f / 200f);
        float uOffset = (0.125f);
        float vFactor = (1f / 200f);
        float vOffset = (0.5f);

        {
            uint i0 = g.GetNextVertexIndex();
            //var ni0 = ng.GetNextVertexIndex();
            g.p(ax + cx, h, ay + cy); g.N(Vector3.UnitY);
            //ng.p(ax + cx, h, ay + cy); ng.N(Vector3.UnitY);
            
            //g.UV(ax * uFactor + uOffset, ay * vFactor + vOffset);
            g.UV(_uvTris[0],_uvTris[1]);
            int uvIndex = 0;
            //ng.UV(0.25f,0.5f);
            foreach (var b in secArray)
            {
                g.p(b.X + cx, h, b.Y + cy); g.N(Vector3.UnitY);
                //ng.p(b.X + cx, h, b.Y + cy); ng.N(Vector3.UnitY);
                //g.UV(b.X * uFactor + uOffset, b.Y * vFactor + vOffset);
                g.UV(_uvTris[2+uvIndex],_uvTris[2+uvIndex+1]);
                uvIndex = (uvIndex + 2) & 3;
                //ng.UV(0.25f,0.5f);
            }

            /*
             * Now create the vertex indices for the triangles.
             */
            for (uint k = 0; k < l; ++k)
            {
                uint knext = (k + 1) % l;
                g.Idx(i0 + 0, i0 + 1 + knext, i0 + 1 + k);
                //ng.Idx(ni0 + 0, ni0 + 1 + knext, ni0 + 1 + k);
            }
        }

        /*
         * That's it!
         */
        return true;
    }


    /**
     * Generate the streets between any junctions.
     */
    private bool _generateStreetRun(
        world.Fragment worldFragment,
        float cx, float cy,
        streets.Stroke stroke,
        Artefact a)
    {
        var g = a.g;
        //var ng = a.ng;
        
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
        Vector2 q = stroke.Unit;

        var spA = stroke.A;

        /*
         * Before continuing, we check whether point a is inside this fragment.
         * By convention, we only create streets that have their a point inside this
         * fragment.
         */
        if (!worldFragment.IsInsideLocal(spA.Pos.X + cx, spA.Pos.Y + cy))
        {
            return false;
        }

        var angArrA = spA.GetAngleArray();

        /*
         * The exterior points of the street area.
         */
        float alx, aly, arx, ary;
        float blx, bly, brx, bry;

        /*
         * The linear logical part of the street.
         */
        float amx, amy;
        float bmx, bmy;

        amx = cx + spA.Pos.X;
        amy = cy + spA.Pos.Y;
        if (_traceStreets) Trace($"am = ({amx}; {amy});");
        if (angArrA.Count > 1)
        {
            var idxA = angArrA.IndexOf(stroke);
            if (idxA < 0)
            {
                ErrorThrow($"stroke is not in street point A.", le => new InvalidOperationException(le));
            }

            var secArrA = spA.GetSectionArray();
            if (secArrA.Count != angArrA.Count)
            {
                ErrorThrow(
                    $"for point a: Section array and length array differ in size: {secArrA.Count} != {angArrA.Count}.",
                    le => new InvalidOperationException());
            }

            var idxNextA = (idxA + 1) % angArrA.Count;

            /*
             * now idxA is the index of this stroke.
             * in secArr A, we will find the intersection of this stroke with the previous
             * one at A, at the next index the intersection of this one with the next.
             */

            /*
             * The angle array is sorted in ascending angles with regard to outgoing
             * strokes, that is stroke.a is the point.
             */
            alx = cx + secArrA[idxNextA].X;
            aly = cy + secArrA[idxNextA].Y;
            arx = cx + secArrA[idxA].X;
            ary = cy + secArrA[idxA].Y;

        }
        else
        {
            /*
             * This is the end of the street, we need to manually compute the endpoints
             * using the street normal.
             */
            alx = cx + spA.Pos.X - n.X * hsw;
            aly = cy + spA.Pos.Y - n.Y * hsw;
            arx = cx + spA.Pos.X + n.X * hsw;
            ary = cy + spA.Pos.Y + n.Y * hsw;

        }

        var spB = stroke.B;
        var angArrB = spB.GetAngleArray();

        bmx = cx + spB.Pos.X;
        bmy = cy + spB.Pos.Y;
        if (_traceStreets) Trace($"bm = ({bmx}; {bmy});");
        if (angArrB.Count > 1)
        {
            var idxB = angArrB.IndexOf(stroke);
            if (idxB < 0)
            {
                ErrorThrow($"stroke is not in street point B.", le => new InvalidOperationException(le));
            }

            var secArrB = spB.GetSectionArray();
            if (secArrB.Count != angArrB.Count)
            {
                ErrorThrow(
                    $"for point b: Section array and angle array differ in size: {secArrB.Count} != {angArrB.Count}.",
                    le => new InvalidOperationException(le));
            }

            var idxNextB = (idxB + 1) % angArrB.Count;

            /*
             * right now there is idxB the index of this stroke in the streetpoint b.
             * From spB's point of view, stroke is an incoming stroke.
             *
             * So this is the end on the street on the "other" side, left and right are
             * from a's point of view. So we have:
             */
            blx = cx + secArrB[idxB].X;
            bly = cy + secArrB[idxB].Y;
            brx = cx + secArrB[idxNextB].X;
            bry = cy + secArrB[idxNextB].Y;

        }
        else
        {
            /*
             * Create a street end from the normals.
             */
            blx = cx + spB.Pos.X - n.X * hsw;
            bly = cy + spB.Pos.Y - n.Y * hsw;
            brx = cx + spB.Pos.X + n.X * hsw;
            bry = cy + spB.Pos.Y + n.Y * hsw;
        }

        var h = _clusterDesc.AverageHeight + world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

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
        var vam = new Vector3(amx, h, amy);
        var vambm = new Vector3(bmx - amx, 0f, bmy - amy);

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
        float dal = Vector3.Dot(new Vector3(alx - amx, 0f, aly - amy), vambm) / vambm.Length();
        float dar = Vector3.Dot(new Vector3(arx - amx, 0f, ary - amy), vambm) / vambm.Length();
        float dbl = Vector3.Dot(new Vector3(blx - amx, 0f, bly - amy), vambm) / vambm.Length();
        float dbr = Vector3.Dot(new Vector3(brx - amx, 0f, bry - amy), vambm) / vambm.Length();

        float damax, damin;
        float dStart = 0f;
        float vStart = 0f;
        if (dal < dar)
        {
            damax = dar;
            damin = dal;

            /*
             * Look, what iteration of texture we start with to offset the v values.
             */
            while ((damin - dStart) < 0f)
            {
                dStart -= texlen;
                vStart -= 1f;
            }

            while ((damin - dStart) > texlen)
            {
                dStart += texlen;
                vStart += 1f;
            }

            {
                /*
                 * Emit triangle at a, run will start at height of dar.
                 * Tri: al, al @ height of dar (cl), ar
                 *
                 * Note that we start from the beginning in the texture.
                 */
                uint i0 = g.GetNextVertexIndex();
                //uint ni0 = ng.GetNextVertexIndex();
                var cm = new Vector3(q.X, 0f, q.Y);
                cm *= dar;
                cm = cm + vam;
                var clx = cm.X - hsw * n.X;
                var cly = cm.Z - hsw * n.Y;
                var uval = uvp.GetUV(new Vector3(alx, h, aly), 0f, vStart);
                var uvcl = uvp.GetUV(new Vector3(clx, h, cly), 0f, vStart);
                var uvar = uvp.GetUV(new Vector3(arx, h, ary), 0f, vStart);
                float vofs = 1.0f - Single.Max(uval.Y, Single.Max(uvar.Y, uvcl.Y));
                g.p(alx, h, aly); g.N(Vector3.UnitY);
                //ng.p(alx, h, aly); ng.N(Vector3.UnitY);
                g.UV(uval.X, uval.Y + vofs);
                //ng.UV(uval.X, uval.Y + vofs);
                g.p(clx, h, cly); g.N(Vector3.UnitY);
                //ng.p(clx, h, cly); ng.N(Vector3.UnitY);
                g.UV(uvcl.X, uvcl.Y + vofs);
                //ng.UV(uvcl.X, uvcl.Y + vofs);
                g.p(arx, h, ary); g.N(Vector3.UnitY);
                //ng.p(arx, h, ary); ng.N(Vector3.UnitY);
                g.UV(uvar.X, uvar.Y + vofs);
                //ng.UV(uvar.X, uvar.Y + vofs);
                g.Idx(i0 + 0, i0 + 1, i0 + 2);
                //ng.Idx(ni0 + 0, ni0 + 1, ni0 + 2);
            }

        }
        else
        {
            damax = dal;
            damin = dar;

            /*
             * Look, what iteration of texture we start with to offset the v values.
             */
            while ((damin - dStart) < 0f)
            {
                dStart -= texlen;
                vStart -= 1f;
            }

            while ((damin - dStart) > texlen)
            {
                dStart += texlen;
                vStart += 1f;
            }

            {
                /*
                 * Emit triangle at a, run wil start at height dal
                 * Tri: ar, al, ar @ height of dal (cr) .
                 *
                 * Note, that we start from the beginning in the texture
                 */
                uint i0 = g.GetNextVertexIndex();
                //uint ni0 = ng.GetNextVertexIndex();
                var cm = new Vector3(q.X, 0f, q.Y);
                cm *= dal;
                cm += vam;
                var crx = cm.X + hsw * n.X;
                var cry = cm.Z + hsw * n.Y;
                var uvar = uvp.GetUV(new Vector3(arx, h, ary), 0f, vStart);
                var uval = uvp.GetUV(new Vector3(alx, h, aly), 0f, vStart);
                var uvcr = uvp.GetUV(new Vector3(crx, h, cry), 0f, vStart);
                float vofs = 1.0f - Single.Max(uvar.Y, Single.Max(uval.Y, uvcr.Y));
                g.p(arx, h, ary); g.N(Vector3.UnitY);
                //ng.p(arx, h, ary); ng.N(Vector3.UnitY);
                g.UV(uvar.X, uvar.Y + vofs);
                //ng.UV(uvar.X, uvar.Y + vofs);
                g.p(alx, h, aly); g.N(Vector3.UnitY);
                //ng.p(alx, h, aly); ng.N(Vector3.UnitY);
                g.UV(uval.X, uval.Y + vofs);
                //ng.UV(uval.X, uval.Y + vofs);
                g.p(crx, h, cry); g.N(Vector3.UnitY);
                //ng.p(crx, h, cry); ng.N(Vector3.UnitY);
                g.UV(uvcr.X, uvcr.Y + vofs);
                //ng.UV(uvcr.X, uvcr.Y + vofs);
                g.Idx(i0 + 0, i0 + 1, i0 + 2);
                //ng.Idx(ni0 + 0, ni0 + 1, ni0 + 2);
            }
        }

        float dbmin, dbmax;
        if (dbl < dbr)
        {
            dbmin = dbl;
            dbmax = dbr;

            /*
             * Look, what iteration of texture we start with to offset the v values.
             */
            while ((dbmin - dStart) < 0f)
            {
                dStart -= texlen;
                vStart -= 1f;
            }

            while ((dbmin - dStart) > texlen)
            {
                dStart += texlen;
                vStart += 1f;
            }

            {
                /*
                 * Emit tri at b. It will be on line of dbmin, reaching out to br.
                 * bl, br, br@dbl
                 *
                 * Note, that we start from the beginning in the texture
                 */
                uint i0 = g.GetNextVertexIndex();
                //uint ni0 = ng.GetNextVertexIndex();
                var cm = new Vector3(q.X, 0f, q.Y);
                cm *= dbl;
                cm += vam;
                var crx = cm.X + hsw * n.X;
                var cry = cm.Z + hsw * n.Y;
                var uvbl = uvp.GetUV(new Vector3(blx, h, bly), 0f, vStart);
                var uvbr = uvp.GetUV(new Vector3(brx, h, bry), 0f, vStart);
                var uvcr = uvp.GetUV(new Vector3(crx, h, cry), 0f, vStart);
                float vofs = -Single.Min(uvbr.Y,Single.Min(uvbl.Y, uvcr.Y));
                g.p(blx, h, bly); g.N(Vector3.UnitY);
                //ng.p(blx, h, bly); ng.N(Vector3.UnitY);
                g.UV(uvbl.X, uvbl.Y + vofs);
                //ng.UV(uvbl.X, uvbl.Y + vofs);
                g.p(brx, h, bry); g.N(Vector3.UnitY);
                //ng.p(brx, h, bry); ng.N(Vector3.UnitY);
                g.UV(uvbr.X, uvbr.Y + vofs);
                //ng.UV(uvbr.X, uvbr.Y + vofs);
                g.p(crx, h, cry); g.N(Vector3.UnitY);
                //ng.p(crx, h, cry); ng.N(Vector3.UnitY);
                g.UV(uvcr.X, uvcr.Y + vofs);
                //ng.UV(uvcr.X, uvcr.Y + vofs);
                g.Idx(i0 + 0, i0 + 1, i0 + 2);
                //ng.Idx(ni0 + 0, ni0 + 1, ni0 + 2);
            }
        }
        else
        {
            dbmin = dbr;
            dbmax = dbl;

            /*
             * Look, what iteration of texture we start with to offset the v values.
             */
            while ((dbmin - dStart) < 0f)
            {
                dStart -= texlen;
                vStart -= 1f;
            }

            while ((dbmin - dStart) > texlen)
            {
                dStart += texlen;
                vStart += 1f;
            }

            {
                /*
                 * Emit tri at b. It will be on line of dbmin, reaching out to bl.
                 * bl@dbr, bl,br
                 *
                 * Note, that we start from the beginning in the texture
                 */
                uint i0 = g.GetNextVertexIndex();
                //uint ni0 = ng.GetNextVertexIndex();
                var cm = new Vector3(q.X, 0f, q.Y);
                cm *= dbr;
                cm += vam;
                var clx = cm.X - hsw * n.X;
                var cly = cm.Z - hsw * n.Y;
                var uvcl = uvp.GetUV(new Vector3(clx, h, cly), 0f, vStart);
                var uvbl = uvp.GetUV(new Vector3(blx, h, bly), 0f, vStart);
                var uvbr = uvp.GetUV(new Vector3(brx, h, bry), 0f, vStart);
                float vofs = -Single.Min(uvbl.Y,Single.Min(uvbr.Y, uvcl.Y));
                g.p(clx, h, cly); g.N(Vector3.UnitY);
                //ng.p(clx, h, cly); ng.N(Vector3.UnitY);
                g.UV(uvcl.X, uvcl.Y + vofs);
                //ng.UV(uvcl.X, uvcl.Y + vofs);
                g.p(blx, h, bly); g.N(Vector3.UnitY);
                //ng.p(blx, h, bly); ng.N(Vector3.UnitY);
                g.UV(uvbl.X, uvbl.Y + vofs);
                //ng.UV(uvbl.X, uvbl.Y + vofs);
                g.p(brx, h, bry); g.N(Vector3.UnitY);
                //ng.p(brx, h, bry); ng.N(Vector3.UnitY);
                g.UV(uvbr.X, uvbr.Y + vofs);
                //ng.UV(uvbr.X, uvbr.Y + vofs);
                g.Idx(i0 + 0, i0 + 1, i0 + 2);
                //ng.Idx(ni0 + 0, ni0 + 1, ni0 + 2);
            }
        }

        if (_traceStreets) Trace($"d[ab][min/max]: {damin}; {damax}; {dbmin}; {dbmax};");

        /*
         * Handle special case of a and b ends overlapping
         */
        if (damax > dbmin)
        {
            if (true || _traceStreets) Trace($"Overlapping ends, no street run.");
            // TXWTODO: Write me.
            return true;
        }

        /*
         * Starting from am, we layout the street in rows.
         * Emit vertex rows until we are at dbmin.
         */
        {
            uint i0 = g.GetNextVertexIndex();
            //uint ni0 = ng.GetNextVertexIndex();

            /*
             * Count the number of rows to add tris.
             */
            int nVertexRows = 0;

            if (_traceStreets) Trace($"New rect list.");

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
                * Emit current row.
                * 
                * Direction of street ... 
                */
                var em = new Vector3(q.X, 0f, q.Y);
                
                /*
                * ... scaled by current offset (i.e. end of start junction)
                */
                em *= currD;
               
                /*
                * ... plus street point A in fragment coordinates with
                * standard height.
                */
                em += vam;
                var elx = em.X - hsw * n.X;
                var ely = em.Z - hsw * n.Y;
                var erx = em.X + hsw * n.X;
                var ery = em.Z + hsw * n.Y;
                var uv0 = uvp.GetUV(new Vector3(elx, h, ely), 0f, vStart);
                var uv1 = uvp.GetUV(new Vector3(erx, h, ery), 0f, vStart);
                if (_traceStreets)
                    Trace(
                        $"#$nVertexRows: el = ({elx}; {ely}); uv = ({uv0.X}; {uv0.Y}); er = ($erx; $ery); uv = ({uv1.X}; {uv1.Y})");

                if (Math.Abs(uv0.Y - 1.0) < 0.00000001)
                {
                    if (_traceStreets) Trace("Too close");
                }

                g.p(elx, h, ely); g.N(Vector3.UnitY);
                g.UV(uv0.X, uv0.Y);
                g.p(erx, h, ery); g.N(Vector3.UnitY);
                g.UV(uv1.X, uv1.Y);
                
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

                /*
                 * Emit next row (we need it twice in the end)
                 */
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

                var fm = new Vector3(q.X, 0f, q.Y);
                fm *= nextD;
                fm += vam;
                var flx = fm.X - hsw * n.X;
                var fly = fm.Z - hsw * n.Y;
                var frx = fm.X + hsw * n.X;
                var fry = fm.Z + hsw * n.Y;
                var uv2 = uvp.GetUV(new Vector3(flx, h, fly), 0f, vStart);
                var uv3 = uvp.GetUV(new Vector3(frx, h, fry), 0f, vStart);
                if (_traceStreets)
                    Trace(
                        $"#{nVertexRows}: fl = ({flx}; {fly}); uv = ({uv2.X}; {uv2.Y}); fr = ({frx}; {fry}); uv = ({uv3.X}; {uv3.Y})");

                g.p(flx, h, fly); g.N(Vector3.UnitY);
                g.UV(uv2.X, uv2.Y);
                g.p(frx, h, fry); g.N(Vector3.UnitY);
                g.UV(uv3.X, uv3.Y);

                ++nVertexRows;
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

            /*
             * Now emit the triangles.
             */
            for (uint row = 0; row < nVertexRows; ++row)
            {
                g.Idx(i0 + row * 4 + 1, i0 + row * 4 + 0, i0 + row * 4 + 2);
                g.Idx(i0 + row * 4 + 1, i0 + row * 4 + 2, i0 + row * 4 + 3);
            }
        }
        return true;
    }


    public void _applyAnyVisibility(Fragment worldFragment)
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        if (_traceStreets) Trace("Obtaining streets.");
        var strokeStore = _clusterDesc.StrokeStore();
        if (_traceStreets) Trace("Have streets.");

        if (_traceStreets)
        {
            Trace($"In terrain '{worldFragment.GetId()}' operator. "
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
            var didCreateStreetRun = _generateStreetRun(
                worldFragment, cx, cz, stroke, artefact);
            if (didCreateStreetRun)
            {
                nIgnoredStrokes++;
            }
            else
            {
                ++nGeneratedStreets;
            }
        }

        /*
         * Create the junctions.
         */
        if (true)
        {
            foreach (var streetPoint in strokeStore.GetStreetPoints())
            {
                _generateJunction(
                    worldFragment, cx, cz, streetPoint, artefact
                );
            }
        }

        if (_traceStreets) Trace($"Created {nGeneratedStreets} strokes, discarded {nIgnoredStrokes}.");

        if (artefact.g.IsEmpty())
        {
            if (_traceStreets) Trace($"Nothing to add at all.");
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
         * Add the entity containing the instanceDesc.
         */
        worldFragment.AddStaticInstance(
            0x00800001, 
            "engine.streets.streets", 
            instanceDesc);
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
                    if (_traceStreets) Trace("Too far away: x=" + _clusterDesc.Pos.X + ", z=" + _clusterDesc.Pos.Z);
                    return;
                }
            }
        }

        if (_traceStreets) Trace($"cluster '{_clusterDesc.Name}' ({_clusterDesc.IdString}) in range");


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

