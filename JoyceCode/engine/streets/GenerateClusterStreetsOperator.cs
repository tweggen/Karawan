using Android.OS;
using engine.world;
using System;
using System.Collections.Generic;
using System.Text;
namespace engine.streets
{
    public class GenerateClusterStreetsOperator : world.IFragmentOperator
    {
        private static void trace( string message ) {
           

        private static bool _useRepeatTexture = false;

        private ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;
        private bool _traceStreets;


        public string fragmentOperatorGetPath()
        {
            return $"5001/GenerateClusterStreetsOperator/{_myKey}/";
        }


        /**
         * Generate a polygon representing the street point.
         */
        private bool _generateJunction(
            in world.Fragment worldFragment,
            float cx, float cy,
            in streets.StreetPoint sp,
            in joyce.Mesh g
        )
        {

            /*
             * We render only street points inside our fragment.
             */
            if( !worldFragment.IsInsideLocal(sp.Pos.X+cx, sp.Pos.Y+cy )) {
                return false;
            }

            /*
             * We simple generate a polygon using the section points as edges.
             * We triangulate it by creating a fan around the middle.
             */
            var secArray = sp.GetSectionArray();
            int l = secArray.Count;
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
            foreach(var p in secArray)
            {
                ax += p.x;
                ay += p.y;
            }
            ax /= l;
            ay /= l;

            /*
             * Now create the vertices and the uv values.
             * We start with a center point.
             */
            float  facu = (1f/ 200f);
            float ofsu = (0.125f);
            float facv = (1f/ 200f);
            float ofsv = (0.5f);

            int i0 = g.GetNextVertexIndex();
            g.p(ax + cx, h, ay + cy);
            g.UV(ax * facu + ofsu, ay * facv + ofsv);
            foreach(var b in secArray)
            {
                g.p(b.x + cx, h, b.y + cy);
                g.UV(b.x * facu + ofsu, b.y * facv + ofsv);
            }

            /*
             * Now create the vertex indices for the triangles.
             */
            for(int k=0; k<l; ++k )
            {
                var knext = (k + 1) % l;
                g.Idx(i0 + 0, i0 + 1 + knext, i0 + 1 + k);
            }

            /*
             * That's it!
             */
            return true;
        }


        /**
         * Generate the streets between any junctions.
         * 
         * @param v 
         *     A array of vertices
         * @param u 
         *     A array of u/v values per vertex
         * @param i 
         *     The indices of the polygons
         */
        private bool _generateStreetRun(
            world.Fragment worldFragment,
            float cx, float cy,
            streets.Stroke stroke,
            joyce.Mesh g)
        {

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
            var sw = stroke.streetWidth();
            var hsw = sw / 2;
            var n: geom.Point = stroke.normal;
            var q: geom.Point = stroke.unit;

            var spA = stroke.a;

            /*
             * Before continueing, we check whether point a is inside this fragment.
             * By convention, we only create streets that have their a point inside this
             * fragment.
             */
            if (!worldFragment.isInsideLocal(spA.pos.x + cx, spA.pos.y + cy))
            {
                return false;
            }

            var angArrA = spA.getAngleArray();

            /*
             * The exterior points of the street area.
             */
            var alx, aly, arx, ary: Float;
            var blx, bly, brx, bry: Float;

            /*
             * The linear logical part of the street.
             */
            var amx, amy: Float;
            var bmx, bmy: Float;

            amx = cx + spA.pos.x;
            amy = cy + spA.pos.y;
            if (_traceStreets) trace('GenerateClusterStreetsOperator.generateStreetRun(): am = ($amx; $amy);');
            if (angArrA.length > 1)
            {
                var idxA = angArrA.indexOf(stroke);
                if (idxA < 0)
                {
                    throw 'GenerateClusterStreetsOperator.generateStreetRun(): stroke is not in street point A.';
                }
                var secArrA = spA.getSectionArray();
                if (secArrA.length != angArrA.length)
                {
                    throw 'GenerateClusterStreetsOperator.generateStreetRun(): for point a: Section array and length array differ in size: ${secArrA.length} != ${angArrA.length}.';
                }
                var idxNextA = (idxA + 1) % angArrA.length;

                /*
                 * now idxA is the index of this stroke.
                 * in secArr A, we will find the intersection of this stroke with the previous 
                 * one at A, at the next index the intersection of this one with the next.
                 */

                /*
                 * The angle array is sorted in ascending angles with regard to outgoing
                 * strokes, that is stroke.a is the point.
                 */
                alx = cx + secArrA[idxNextA].x;
                aly = cy + secArrA[idxNextA].y;
                arx = cx + secArrA[idxA].x;
                ary = cy + secArrA[idxA].y;

            }
            else
            {
                /*
                 * This is the end of the street, we need to manually compute the endpoints
                 * using the street normal.
                 */
                alx = cx + spA.pos.x - n.x * hsw;
                aly = cy + spA.pos.y - n.y * hsw;
                arx = cx + spA.pos.x + n.x * hsw;
                ary = cy + spA.pos.y + n.y * hsw;

            }

            var spB = stroke.b;
            var angArrB = spB.getAngleArray();

            bmx = cx + spB.pos.x;
            bmy = cy + spB.pos.y;
            if (_traceStreets) trace('GenerateClusterStreetsOperator.generateStreetRun(): bm = ($bmx; $bmy);');
            if (angArrB.length > 1)
            {
                var idxB = angArrB.indexOf(stroke);
                if (idxB < 0)
                {
                    throw 'GenerateClusterStreetsOperator.generateStreetRun(): stroke is not in street point B.';
                }
                var secArrB = spB.getSectionArray();
                if (secArrB.length != angArrB.length)
                {
                    throw 'GenerateClusterStreetsOperator.generateStreetRun(): for point b: Section array and angle array differ in size: ${secArrB.length} != ${angArrB.length}.';
                }
                var idxNextB = (idxB + 1) % angArrB.length;

                /*
                 * right now there is idxB the index of this stroke in the streetpoint b.
                 * From spB's point of view, stroke is an incoming stroke.
                 * 
                 * So this is the end on the street on the "other" side, left and right are
                 * from a's point of view. So we have:
                 */
                blx = cx + secArrB[idxB].x;
                bly = cy + secArrB[idxB].y;
                brx = cx + secArrB[idxNextB].x;
                bry = cy + secArrB[idxNextB].y;

            }
            else
            {
                /*
                 * Create a street end from the normals.
                 */
                blx = cx + spB.pos.x - n.x * hsw;
                bly = cy + spB.pos.y - n.y * hsw;
                brx = cx + spB.pos.x + n.x * hsw;
                bry = cy + spB.pos.y + n.y * hsw;
            }

            var h = _clusterDesc.averageHeight + WorldMetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

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
            var vam = new Vector3D(amx, h, amy);
            var vambm = new Vector3D(bmx - amx, 0., bmy - amy);

            /*
             * Emit tris for am-bm.
             * 
             * Street layout:
             * The texture width is applied to the whole street width.
             * Therefore, the street texture lasts 4 times its width.
             */
            var texlen = stroke.streetWidth() * 4.;

            /*
             * So we initialize our uv projector with uv origin at am - half street width.
             * (left side of street at am).
             */
            var uvp = new geom.UVProjector(
                new Vector3D(vam.x - n.x * hsw, h, vam.z - n.y * hsw),
                new Vector3D(n.x * sw * 4., 0., n.y * sw * 4. ), // That is the logical size of the u [0..1[ interval.
                new Vector3D(q.x * texlen, 0., q.y * texlen));


            /*
             * These are the 4 edge points of the street, projected to street,
             * unit is meters.
             */
            var dal = new Vector3D(alx - amx, 0, aly - amy).project(vambm);
            var dar = new Vector3D(arx - amx, 0, ary - amy).project(vambm);
            var dbl = new Vector3D(blx - amx, 0, bly - amy).project(vambm);
            var dbr = new Vector3D(brx - amx, 0, bry - amy).project(vambm);

            var damax, damin: Float;
            var dStart = 0.;
            var vStart = 0.;
            if (dal < dar)
            {
                damax = dar;
                damin = dal;


                /*
                 * Look, what iteration of texture we start with to offset the v values.
                 */
                while ((damin - dStart) < 0. )
                {
                    dStart -= texlen;
                    vStart -= 1.;
                }
                while ((damin - dStart) > texlen)
                {
                    dStart += texlen;
                    vStart += 1.;
                }

                /*
                 * Emit triangle at a, run will start at height of dar.
                 * Tri: al, al @ height of dar (cl), ar
                 * 
                 * Note that we start from the beginning in the texture.
                 */
                var i0: Int = g.getNextVertexIndex();
                var cm = new Vector3D(q.x, 0., q.y); cm.scale(dar); cm.add(vam);
                var clx = cm.x - hsw * n.x;
                var cly = cm.z - hsw * n.y;
                var uval = uvp.getUVOfs(new Vector3D(alx, h, aly), 0., vStart);
                var uvcl = uvp.getUVOfs(new Vector3D(clx, h, cly), 0., vStart);
                var uvar = uvp.getUVOfs(new Vector3D(arx, h, ary), 0., vStart);
                var vofs = 1.0 - uvar.y;
                g.p(alx, h, aly); g.uv(0.5 + uval.x, uval.y + vofs);
                g.p(clx, h, cly); g.uv(0.5 + uvcl.x, uvcl.y + vofs);
                g.p(arx, h, ary); g.uv(0.5 + uvar.x, uvar.y + vofs);
                g.idx(i0 + 0, i0 + 1, i0 + 2);

            }
            else
            {
                damax = dal;
                damin = dar;

                /*
                 * Look, what iteration of texture we start with to offset the v values.
                 */
                while ((damin - dStart) < 0. )
                {
                    dStart -= texlen;
                    vStart -= 1.;
                }
                while ((damin - dStart) > texlen)
                {
                    dStart += texlen;
                    vStart += 1.;
                }

                /*
                 * Emit triangle at a, run wil start at height dal
                 * Tri: ar, al, ar @ height of dal (cr) .
                 * 
                 * Note, that we start from the beginning in the texture
                 */
                var i0: Int = g.getNextVertexIndex();
                var cm = new Vector3D(q.x, 0., q.y); cm.scale(dal); cm.add(vam);
                var crx = cm.x + hsw * n.x;
                var cry = cm.z + hsw * n.y;
                var uvar = uvp.getUVOfs(new Vector3D(arx, h, ary), 0., vStart);
                var uval = uvp.getUVOfs(new Vector3D(alx, h, aly), 0., vStart);
                var uvcr = uvp.getUVOfs(new Vector3D(crx, h, cry), 0., vStart);
                var vofs = 1.0 - uval.y;
                g.p(arx, h, ary); g.uv(0.5 + uvar.x, uvar.y + vofs);
                g.p(alx, h, aly); g.uv(0.5 + uval.x, uval.y + vofs);
                g.p(crx, h, cry); g.uv(0.5 + uvcr.x, uvcr.y + vofs);
                g.idx(i0 + 0, i0 + 1, i0 + 2);
            }

            var dbmin, dbmax: Float;
            if (dbl < dbr)
            {
                dbmin = dbl;
                dbmax = dbr;

                /*
                 * Look, what iteration of texture we start with to offset the v values.
                 */
                while ((dbmin - dStart) < 0. )
                {
                    dStart -= texlen;
                    vStart -= 1.;
                }
                while ((dbmin - dStart) > texlen)
                {
                    dStart += texlen;
                    vStart += 1.;
                }

                /*
                 * Emit tri at b. It will be on line of dbmin, reaching out to br.
                 * bl, br, br@dbl
                 * 
                 * Note, that we start from the beginning in the texture
                 */
                var i0: Int = g.getNextVertexIndex();
                var cm = new Vector3D(q.x, 0., q.y); cm.scale(dbl); cm.add(vam);
                var crx = cm.x + hsw * n.x;
                var cry = cm.z + hsw * n.y;
                var uvbl = uvp.getUVOfs(new Vector3D(blx, h, bly), 0., vStart);
                var uvbr = uvp.getUVOfs(new Vector3D(brx, h, bry), 0., vStart);
                var uvcr = uvp.getUVOfs(new Vector3D(crx, h, cry), 0., vStart);
                var vofs = -uvbl.y;
                g.p(blx, h, bly); g.uv(0.5 + uvbl.x, uvbl.y + vofs);
                g.p(brx, h, bry); g.uv(0.5 + uvbr.x, uvbr.y + vofs);
                g.p(crx, h, cry); g.uv(0.5 + uvcr.x, uvcr.y + vofs);
                g.idx(i0 + 0, i0 + 1, i0 + 2);
            }
            else
            {
                dbmin = dbr;
                dbmax = dbl;

                /*
                 * Look, what iteration of texture we start with to offset the v values.
                 */
                while ((dbmin - dStart) < 0. )
                {
                    dStart -= texlen;
                    vStart -= 1.;
                }
                while ((dbmin - dStart) > texlen)
                {
                    dStart += texlen;
                    vStart += 1.;
                }

                /*
                 * Emit tri at b. It will be on line of dbmin, reaching out to bl.
                 * bl@dbr, bl,br
                 * 
                 * Note, that we start from the beginning in the texture
                 */
                var i0: Int = g.getNextVertexIndex();
                var cm = new Vector3D(q.x, 0., q.y); cm.scale(dbr); cm.add(vam);
                var clx = cm.x - hsw * n.x;
                var cly = cm.z - hsw * n.y;
                var uvcl = uvp.getUVOfs(new Vector3D(clx, h, cly), 0., vStart);
                var uvbl = uvp.getUVOfs(new Vector3D(blx, h, bly), 0., vStart);
                var uvbr = uvp.getUVOfs(new Vector3D(brx, h, bry), 0., vStart);
                var vofs = -uvbr.y;
                g.p(clx, h, cly); g.uv(0.5 + uvcl.x, uvcl.y + vofs);
                g.p(blx, h, bly); g.uv(0.5 + uvbl.x, uvbl.y + vofs);
                g.p(brx, h, bry); g.uv(0.5 + uvbr.x, uvbr.y + vofs);
                g.idx(i0 + 0, i0 + 1, i0 + 2);
            }

            if (_traceStreets) trace('GenerateClusterStreetsOperator.generateStreetRun(): d[ab][min/max]: $damin; $damax; $dbmin; $dbmax;');

            /*
             * Handle special case of a and b ends overlapping
             */
            if (damax > dbmin)
            {
                if (_traceStreets) trace('GenerateClusterStreetsOperator.generateStreetRun(): Overlapping ends, no street run.');
                // TXWTODO: Write me.
                return true;
            }

            /* 
             * Starting from am, we layout the street in rows.
             * Emit vertex rows until we are at dbmin.
             */

            var i0: Int = g.getNextVertexIndex();

            /*
             * Count the number of rows to add tris.
             */
            var nVertexRows: Int = 0;

            if (_traceStreets) trace('GenerateClusterStreetsOperator.generateStreetRun(): New rect list.');

            /*
             * We start at damax.
             */
            var currD = damax;
            var finalD = dbmin;

            dStart = 0.;
            vStart = 0.;
            /*
             * Or the other way round?
             */
            while ((currD - dStart) < 0. )
            {
                dStart -= texlen;
                vStart -= 1.;
            }
            while ((currD - vStart) > texlen)
            {
                dStart += texlen;
                vStart += 1.;
            }
            while (true)
            {

                /*
                 * Emit current row.
                 */
                var em = new Vector3D(q.x, 0., q.y); em.scale(currD); em.add(vam);
                var elx = em.x - hsw * n.x;
                var ely = em.z - hsw * n.y;
                var erx = em.x + hsw * n.x;
                var ery = em.z + hsw * n.y;
                var uv0 = uvp.getUVOfs(new Vector3D(elx, h, ely), 0., vStart);
                var uv1 = uvp.getUVOfs(new Vector3D(erx, h, ery), 0., vStart);
                if (_traceStreets) trace('GenerateClusterStreetsOperator(): #$nVertexRows: el = ($elx; $ely); uv = (${uv0.x}; ${uv0.y}); er = ($erx; $ery); uv = (${uv1.x}; ${uv1.y})');

                if (Math.abs(uv0.y - 1.0) < 0.00000001)
                {
                    if (_traceStreets) trace('Too close');
                }
                g.p(elx, h, ely); g.uv(0.5 + uv0.x, uv0.y);
                g.p(erx, h, ery); g.uv(0.5 + uv1.x, uv1.y);

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
                var nextD: Float;
                {
                    var nextWholeD = Math.fceil(currD / texlen) * texlen;
                    if ((nextWholeD - currD) < 0.001)
                    {
                        nextWholeD = nextWholeD + texlen;
                    }
                    nextD = Math.min(nextWholeD, finalD);
                }

                var fm = new Vector3D(q.x, 0., q.y); fm.scale(nextD); fm.add(vam);
                var flx = fm.x - hsw * n.x;
                var fly = fm.z - hsw * n.y;
                var frx = fm.x + hsw * n.x;
                var fry = fm.z + hsw * n.y;
                var uv2 = uvp.getUVOfs(new Vector3D(flx, h, fly), 0., vStart);
                var uv3 = uvp.getUVOfs(new Vector3D(frx, h, fry), 0., vStart);
                if (_traceStreets) trace('GenerateClusterStreetsOperator(): #$nVertexRows: fl = ($flx; $fly); uv = (${uv2.x}; ${uv2.y}); fr = ($frx; $fry); uv = (${uv3.x}; ${uv3.y})');

                g.p(flx, h, fly); g.uv(0.5 + uv2.x, uv2.y);
                g.p(frx, h, fry); g.uv(0.5 + uv3.x, uv3.y);

                ++nVertexRows;
                vStart += 1;

                /*
                 * Finish, if we already reached finalD.
                 */
                if (nextD == finalD)
                {
                    break;
                }


                // TODO: Small adjustment: If nextD is too close to finalD but not equal, set it to finalD.

                currD = nextD;
            }

            /*
             * Now emit the triangles.
             */
            for (row in 0...nVertexRows )
            {
                g.idx(i0 + row * 4 + 1, i0 + row * 4 + 0, i0 + row * 4 + 2);
                g.idx(i0 + row * 4 + 1, i0 + row * 4 + 2, i0 + row * 4 + 3);
            }

            return true;
        }


        /**
         * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
         */
        public function fragmentOperatorApply(
            allEnv: AllEnv,
                worldFragment: WorldFragment
            ) : Void
        {
            // Perform clipping until we have bounding boxes

            var cx:Float = _clusterDesc.x - worldFragment.x;
            var cz:Float = _clusterDesc.z - worldFragment.z;

            /*
             * We don't apply the operator if the fragment completely is
             * outside our boundary box (the cluster)
             */
            {
                {
                    var csh: Float = _clusterDesc.size / 2.0;
                    var fsh: Float = WorldMetaGen.fragmentSize / 2.0;
                    if (
                        (cx - csh) > (fsh)
                        || (cx + csh) < (-fsh)
                        || (cz - csh) > (fsh)
                        || (cz + csh) < (-fsh)
                    )
                    {
                        if (_traceStreets) trace("Too far away: x=" + _clusterDesc.x + ", z=" + _clusterDesc.z);
                        return;
                    }
                }
            }

            trace('GenerateClusterStreetsOperator(): cluster "${_clusterDesc.name}" (${_clusterDesc.id}) in range');
            if (_traceStreets) trace('GenerateClusterStreetsOperator(): Obtaining streets.');
            var strokeStore = _clusterDesc.strokeStore();
            if (_traceStreets) trace('GenerateClusterStreetsOperator(): Have streets.');

            if (_traceStreets) trace('GenerateClusterStreetsOperator(): In terrain "${worldFragment.getId()}" operator. '
                + 'Fragment @${worldFragment.x}, ${worldFragment.z}. '
                + 'Cluster "${_clusterDesc.id}" @$cx, $cz, R:${_clusterDesc.size}.');

            /*
             * We need the coordinates of the cluster relative to the fragment to translate 
             * everything to fragment coorddinates.
             */

            var nGeneratedStreets = 0;
            var nIgnoredStrokes = 0;

            worldFragment.addMaterialFactory(
                "GenerateClusterStreetsOperator._matStreet", function() {
                var mat = new engine.Material("");
                mat.diffuseTexturePath = "street/streets1to4.png";
                // mat.textureRepeat = true;
                mat.textureRepeat = false;
                mat.textureSmooth = true;
                mat.ambientColor = 0xffffff;
                mat.ambient = 0.5;
                mat.specular = 0.0;
                return mat;
            }
                );

            var g = new engine.PlainGeomAtom(null, null, null,
                "GenerateClusterStreetsOperator._matStreet");

            /*
             * Create the roads between the junctions.
             */
            for (stroke in strokeStore.getStrokes())
            {
                var didCreateStreetRun = generateStreetRun(
                    worldFragment, cx, cz, stroke,
                    g);
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
                for (streetPoint in strokeStore.getStreetPoints() )
                {
                    generateJunction(
                        worldFragment, cx, cz, streetPoint, g
                    );
                }
            }

            trace('GenerateClusterStreetsOperator(): Created $nGeneratedStreets strokes, discarded $nIgnoredStrokes.');

            if (g.isEmpty())
            {
                if (_traceStreets) trace('GenerateClusterStreetsOperator(): Nothing to add at all.');
                return;
            }

            var mol = new engine.SimpleMolecule( [g] );
            worldFragment.addStaticMolecule(mol);

        }


public function new(
    clusterDesc: ClusterDesc,
    strKey: String
)
{
    _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);
    }
}
    }
}
