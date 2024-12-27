using engine.world;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
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
            Trace($"uv out of range: {uv}.");
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
            Trace($"uvb {uvb} too close to uva {uva}.");
            result = false;
        }
        if ( (uvc-uvb).LengthSquared()<0.000001f )
        {
            Trace($"uvc {uvc} too close to uvb {uvb}.");
            result = false;
        }
        if ( (uva-uvc).LengthSquared()<0.000001f )
        {
            Trace($"uva {uva} too close to uvc {uvc}.");
            result = false;
        }

        return result;
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
    private bool _generateStreetRun(
        world.Fragment worldFragment,
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
        var h = _clusterDesc.AverageHeight + world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;
        Vector3 v3Cluster = new(cx, h, cy);
        

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
        Vector3 al, ar, bl, br;

        /*
         * The linear logical part of the street.
         */
        Vector3 am, bm;

        am = v3Cluster + new Vector3(spA.Pos.X, 0f, spA.Pos.Y);
        if (_traceStreets) Trace($"am = ({am});");
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
            al = v3Cluster + new Vector3(secArrA[idxNextA].X, 0f, secArrA[idxNextA].Y);
            ar = v3Cluster + new Vector3(secArrA[idxA].X, 0f, secArrA[idxA].Y);

        }
        else
        {
            /*
             * This is the end of the street, we need to manually compute the endpoints
             * using the street normal.
             */
            al = v3Cluster + new Vector3(spA.Pos.X, 0f, spA.Pos.Y) - n3 * hsw;
            ar = v3Cluster + new Vector3(spA.Pos.X, 0f, spA.Pos.Y) + n3 * hsw;

        }

        var spB = stroke.B;
        var angArrB = spB.GetAngleArray();

        bm = v3Cluster + new Vector3(spB.Pos.X, 0f, spB.Pos.Y);

        if (_traceStreets) Trace($"bm = ({bm});");
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
            bl = v3Cluster + new Vector3(secArrB[idxB].X, 0f, secArrB[idxB].Y);
            br = v3Cluster + new Vector3(secArrB[idxNextB].X, 0f, secArrB[idxNextB].Y);
        }
        else
        {
            /*
             * Create a street end from the normals.
             */
            bl = v3Cluster + new Vector3(spB.Pos.X, 0f, spB.Pos.Y) - n3 * hsw;
            br = v3Cluster + new Vector3(spB.Pos.X, 0f, spB.Pos.Y) + n3 * hsw;
        }

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

        if (_traceStreets) Trace($"d[ab][min/max]: {damin}; {damax}; {dbmin}; {dbmax};");

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
            uint i0 = g.GetNextVertexIndex();

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

