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


    /**
     * How far away a fragment's block floors are still drawn.
     *
     * This was 400 m - less than the diagonal of the very fragment the floors belong to -
     * while GenerateClusterStreetsOperator emits the roads at 100000 m and the terrain
     * mesh at 3000 m. So the roads ran to the horizon over ground with no pavements on
     * it, which is half of what "I'm only seeing very few sidewalks" looks like; the
     * other half is Triangulate.ToMesh.
     *
     * Derived rather than picked. DrawInstancesSystem culls on the distance from the
     * camera to the instance's origin - a fragment's origin here - and PlayerViewer keeps
     * fragments within LoadNSurroundingFragments in each axis, so the furthest a loaded
     * fragment's origin can be is (N + 1/2) fragments away along each axis, with the half
     * being the camera's own offset inside its fragment. Anything at least that far
     * cannot cull a fragment the loader has decided to keep - which is the property
     * worth having, since a fragment that is loaded has already paid for its geometry.
     * Costs nothing to draw: a whole fragment's block floors merge to one mesh of a few
     * hundred vertices (measured: 339 vertices, 765 indices at the worst fragment of the
     * 3000 m city).
     *
     * One fragment of slack on top, so that the bound is not a knife edge on a distance
     * the renderer computes in single precision from a camera position it also uses for
     * everything else.
     */
    public static float MaxDrawDistance =>
        (world.PlayerViewer.LoadNSurroundingFragments + 1.5f)
        * world.MetaGen.FragmentSize * Single.Sqrt(2f);


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
     * it: pairing the corner with a neighbouring delimiter's junction compiles, leaves a
     * flat city bit for bit identical, and takes the height of a junction 70 to 97 m away.
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


    /**
     * The inner edge of this block's pavement, or null for a block that keeps a plain fan.
     *
     * Without it the block floor is a single triangle fan spanning kerb to kerb, so on a
     * slope the pavement falls about 11 % ACROSS its width at the median - tipping toward
     * the road as often as away, and steeper sideways than lengthwise on more than half of
     * all block edges. With it, the strip between the two rings is level across by
     * construction and the warp is confined to the block's interior, where the buildings
     * are. See generation.SidewalkRing.
     *
     * **A flat city gets none.** Every corner of a flat block is at the same height, so
     * there is no cross-fall to remove and an inset ring would only add vertices to a mesh
     * this whole line of work has kept bit for bit stable. Gated the same way
     * Quarter.GroundHeightAt, DeckCollider and JunctionCollider are.
     *
     * Hoisted next to FloorOutlineOf for the reason that one was: inline is where nothing
     * can check which side of the kerb the ring came out on.
     */
    internal static List<builtin.tools.CapInsetEdge> PavementInsetOf(
        streets.Quarter quarter, in IList<Vector3> outline)
    {
        if (quarter.ClusterDesc.StreetHeightSource.IsFlat)
        {
            return null;
        }

        return generation.SidewalkRing.InsetOf(outline, quarter.SidewalkWidth);
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
            Warning(_dc, $"A block of cluster '{_clusterDesc.Name}' has only "
                         + $"{edges.Count} corners and gets no floor.");
            return false;
        }

        Mesh meshGround = new($"{worldFragment.GetId()}-quarterfloor");
        var opExtrudePoly = new builtin.tools.ExtrudePoly(edges, path, 27, 10000f, false, false, true)
        {
            CapInsetEdges = PavementInsetOf(quarter, edges)
        };
        try
        {
            opExtrudePoly.BuildGeom(meshGround);
            matmesh.Add(I.Get<ObjectRegistry<Material>>().Get("engine.streets.materials.cluster"), meshGround);
        }
        catch (Exception e)
        {
            /*
             * Error and not Trace. A block that fails to build is a block the player
             * cannot see, and Trace is filtered off by default - so this used to be
             * completely silent, which is how "there are no logs" gets read as "nothing
             * is wrong". Error and Warning are never filtered.
             */
            Error(_dc, $"Unable to build the floor geometry of a block in "
                       + $"'{FragmentOperatorGetPath()}': {e}");
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
            Error(_dc, $"Unable to build the collision surface of a block in "
                       + $"'{FragmentOperatorGetPath()}': {e}");
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
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmmerged, MaxDrawDistance);
            worldFragment.AddStaticInstance("engine.streets.quarters", id, listCreatePhysics);
        }
        catch (Exception e)
        {
            Error(_dc, $"Unable to emit the block floors of "
                       + $"'{FragmentOperatorGetPath()}': {e}");
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