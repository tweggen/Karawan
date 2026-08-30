using engine.joyce;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using engine.physics;
using static engine.Logger;

namespace engine.streets;

/**
 * Create the 3d geometry for cluster floors.
 */
public class GenerateClusterQuartersOperator : world.IFragmentOperator
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    static private object _lock = new();
    private world.ClusterDesc _clusterDesc;
    private builtin.tools.RandomSource _rnd;
    private string _myKey;
    private bool _traceQuarters = false;


    public string FragmentOperatorGetPath()
    {
        return $"5010/GenerateClusterQuartersOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    /**
     * The block floor's outline: one vertex per boundary corner, at the height of the
     * road that corner meets.
     *
     * The extrusion path adds QuarterSidewalkOffset on top of this, and the top face is
     * the pavement - so the kerb is exactly that offset above the carriageway at every
     * corner of every block, which is the whole point of taking the corner's own junction
     * height rather than the pad's value there.
     *
     * That makes the outline non-planar wherever the block's corners are not coplanar,
     * which on any slope is everywhere. LibTess keeps every vertex's own height and
     * invents no vertices (TriangulateNonPlanarTests), and BuildStaticPhys builds convex
     * hulls, so neither minds.
     *
     * Here rather than inline in the emission because inline is where nothing can check
     * it: pairing the corner with its own delimiter's StreetPoint compiles, leaves a flat
     * city bit for bit identical, and takes the height of a junction 70 to 97 m away.
     *
     * @param cx, cy
     *     Where the cluster's origin lands in the fragment.
     */
    internal static List<Vector3> FloorOutlineOf(streets.Quarter quarter, float cx, float cy)
    {
        var delimList = quarter.GetDelims();
        List<Vector3> edges = new(delimList.Count);

        /*
         * The quarters are clockwise, the extrude operator expects them counterclockwise.
         * That happens automatically due to the coordinate change (from y to z).
         */
        for (int i = 0; i < delimList.Count; i++)
        {
            var delim = delimList[i];

            float h = quarter.CornerGroundHeightAt(delim)
                      + world.MetaGen.ClusterStreetHeight;

            edges.Add(new Vector3(cx + delim.StartPoint.X, h, cy + delim.StartPoint.Y));
        }

        return edges;
    }


    private bool _generateQuarterFloor(
        world.Fragment worldFragment,
        MatMesh matmesh,
        streets.Quarter quarter,
        float cx,
        float cy,
        in IList<Func<IList<StaticHandle>, Action>> listCreatePhysics
    )
    {
        List<Vector3> path = new();

        path.Add(new Vector3(0f, world.MetaGen.QuarterSidewalkOffset, 0f));
        var delimList = quarter.GetDelims();

        var edges = FloorOutlineOf(quarter, cx, cy);

        if (edges.Count < 3)
        {
            Trace(_dc, $"No delims found");
            return false;
        }

        Mesh meshGround = new($"{worldFragment.GetId()}-quarterfloor");
        var opExtrudePoly = new builtin.tools.ExtrudePoly(edges, path, 27, 10000f, false, false, true);
        try
        {
            opExtrudePoly.BuildGeom(meshGround);
            matmesh.Add(I.Get<ObjectRegistry<Material>>().Get("engine.streets.materials.cluster"), meshGround);
        }
        catch (Exception e)
        {
            Trace(_dc, $"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
        }

        CollisionProperties props = new(){
            Flags = 
                CollisionProperties.CollisionFlags.IsTangible 
                | CollisionProperties.CollisionFlags.IsDetectable,
            Name = $"quarterfloor-{new Vector3(delimList[0].StartPoint.X, 0f, delimList[0].StartPoint.Y)+worldFragment.Position}",
            SolidLayerMask = CollisionProperties.Layers.Terrain,
            SensitiveLayerMask = 0
        };
        try
        {
            var fCreatePhysics = opExtrudePoly.BuildStaticPhys(worldFragment, props);
            listCreatePhysics.Add(fCreatePhysics);
        }
        catch (Exception e)
        {
            Trace(_dc, $"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
        }

        return true;
    }


    /**
     * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
     */
    public Func<Task> FragmentOperatorApply(world.Fragment worldFragment, engine.world.FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }
        
        _rnd = new builtin.tools.RandomSource(_myKey);

        // Perform clipping until we have bounding boxes

        /*
         * cx/cz is the position of the cluster relative to the fragment.
         * The geometry is generated relative to the fragment.
         */
        Vector3 c = _clusterDesc.Pos - worldFragment.Position;
        float cx = c.X;
        float cz = c.Z;

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
                    // trace( "Too far away: x="+_clusterDesc.x+", z="+_clusterDesc.z);
                    return;
                }
            }
        }

        if (_traceQuarters) Trace(_dc, $"Cluster '{_clusterDesc.Name}' ({_clusterDesc.IdString}) in range");

        MatMesh matmesh = new();
        List<Func<IList<StaticHandle>, Action>> listCreatePhysics = new();

        /*
         * Now iterate through all quarters of this cluster.
         * We only generate quarters that have their centers within this
         * fragment.
         */
        var quarterStore = _clusterDesc.QuarterStore();
        foreach (var quarter in quarterStore.GetQuarters())
        {
            try
            {
                /*
                 * Is the quarter part of this fragment?
                 */
                Vector2 center = quarter.GetCenterPoint();
                center += new Vector2(_clusterDesc.Pos.X, _clusterDesc.Pos.Z);
                if (!worldFragment.IsInside(center))
                {
                    // This is outside, continue;
                    continue;
                }
            }
            catch (Exception e)
            {
                Warning(_dc, $"Unknown exception: {e}");
            }

            _generateQuarterFloor(worldFragment, matmesh, quarter, cx, cz, listCreatePhysics);
        }

        if (matmesh.IsEmpty())
        {
            if (_traceQuarters) Trace(_dc, $"Nothing to add at all.");
            return;
        }

        try
        {
            // TXWTODO: Merge this, this is inefficient.
            var mmmerged = MatMesh.CreateMerged(matmesh);
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmmerged, 400f);
            worldFragment.AddStaticInstance("engine.streets.quarters", id, listCreatePhysics);
        }
        catch (Exception e)
        {
            Trace(_dc, $"Unknown exception: {e}");
        }

    });


    public GenerateClusterQuartersOperator(
        in world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;

        I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.streets.materials.cluster",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff262222)
            });
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateClusterQuartersOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}