#if false

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace nogame.cities;

public class GenerateHouseDescriptionsOperator : engine.world.IFragmentOperator
{
    static private object _lo = new();

    private engine.world.ClusterDesc _clusterDesc;
    private builtin.tools.RandomSource _rnd;
    private string _myKey;
    
    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateHousesOperator/{_myKey}/{_clusterDesc.Id}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment) => new(async () =>
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        float fsh = engine.world.MetaGen.FragmentSize / 2.0f;

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            float csh = _clusterDesc.Size / 2.0f;
            if (
                (cx - csh) > (fsh)
                || (cx + csh) < (-fsh)
                || (cz - csh) > (fsh)
                || (cz + csh) < (-fsh)
            )
            {
                return;
            }
        }

        // trace( 'GenerateHousesOperator(): cluster "${_clusterDesc.name}" (${_clusterDesc.id}) in range');
        _rnd.Clear();

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();

        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace($"Skipping invalid quarter.");
                continue;
            }

            /*
             * Place on house in each quarter in the middle.
             */
            float xmiddle = 0.0f;
            float ymiddle = 0.0f;
            int n = 0;
            var delims = quarter.GetDelims();
            foreach (var delim in delims)
            {
                xmiddle += delim.StreetPoint.Pos.X;
                ymiddle += delim.StreetPoint.Pos.Y;
                ++n;
            }

            // trace( 'middle: $xmiddle, $ymiddle');
            if (3 > n)
            {
                continue;
            }

            xmiddle /= n;
            ymiddle /= n;

            /*
             * Compute some properties of this quarter.
             * - is it convex?
             * - what is it extend?
             * - what is the largest side?
             */
            foreach (var estate in quarter.GetEstates())
            {

                /*
                 * Now create a house subgeometry for each of the buildings on the
                 * estate.
                 */
                foreach (var building in estate.GetBuildings())
                {
                    var orgCenter = building.getCenter();
                    var center = orgCenter;
                    center.X += cx;
                    center.Z += cz;
                    if (!worldFragment.IsInsideLocal(center.X, center.Z))
                    {
                        // trace( 'No building ${orgCenter.x}, ${orgCenter.z} (abs ${center.x}, ${center.z})' );
                        continue;
                    }
                    else
                    {
                        // trace( 'Building at ${orgCenter.x}, ${orgCenter.z} (abs ${center.x}, ${center.z})' );
                    }

                    var orgPoints = building.GetPoints();
                    var fragPoints = new List<Vector3>();
                    foreach (var p in orgPoints)
                    {
                        fragPoints.Add(
                            new Vector3(
                                p.X + cx,
                                _clusterDesc.AverageHeight + 2.15f,
                                p.Z + cz
                            )
                        );
                    }

                    var height = building.GetHeight();

                }
            }
        }
    });


    public GenerateHouseDescriptionsOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey)
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new builtin.tools.RandomSource(strKey);
    }
    

    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateHouseDescriptionsOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
#endif