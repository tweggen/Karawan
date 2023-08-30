using engine.joyce;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.streets;

public class GenerateClusterQuartersOperator : world.IFragmentOperator
{
    static private object _lock = new();
    private world.ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;
    private bool _traceQuarters = false;


    public string FragmentOperatorGetPath()
    {
        return $"5010/GenerateClusterQuartersOperator/{_myKey}/{_clusterDesc.Id}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private bool _generateQuarterFloor(
        world.Fragment worldFragment,
        MatMesh matmesh,
        streets.Quarter quarter,
        float cx,
        float cy
    )
    {
        List<Vector3> edges = new();
        List<Vector3> path = new();

        path.Add(new Vector3(0f, 0.15f, 0f));
        var delimList = quarter.GetDelims();
        int n = 0;

        float h = _clusterDesc.AverageHeight + 2f;

        /*
         * Create the main poly, plus the edges.
         */
        foreach (var delim in delimList)
        {
            ++n;
            edges.Add(new Vector3(cx + delim.StartPoint.X, h, cy + delim.StartPoint.Y));
        }

        if (n < 3)
        {
            Trace("No delims found");
            return false;
        }

        Mesh meshGround = new($"{worldFragment.GetId()}-quarterfloor");
        var opExtrudePoly = new builtin.tools.ExtrudePoly(edges, path, 27, 10000f, false, false, true);
        try
        {
            opExtrudePoly.BuildGeom(meshGround);
            matmesh.Add(MaterialCache.Get("engine.streets.materials.cluster"), meshGround);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
        }

        return true;
    }


    /**
     * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
     */
    public Task FragmentOperatorApply(world.Fragment worldFragment) => new Task(() =>
    {
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

        if (_traceQuarters) Trace($"Cluster '{_clusterDesc.Name}' ({_clusterDesc.Id}) in range");

        MatMesh matmesh = new();

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
                Warning($"Unknown exception: {e}");
            }

            _generateQuarterFloor(worldFragment, matmesh, quarter, cx, cz);
        }

        if (matmesh.IsEmpty())
        {
            if (_traceQuarters) Trace($"Nothing to add at all.");
            return;
        }

        try
        {
            // TXWTODO: Merge this, this is inefficient.
            var mmmerged = MatMesh.CreateMerged(matmesh);
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmmerged, 400f);
            worldFragment.AddStaticInstance("engine.streets.quarters", id);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception: {e}");
        }

    });


    public GenerateClusterQuartersOperator(
        in world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);

        MaterialCache.Register("engine.streets.materials.cluster",
            name => new Material()
            {
                //AlbedoColor = 0xff441144
                AlbedoColor = 0xff262222
            });
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateClusterQuartersOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}