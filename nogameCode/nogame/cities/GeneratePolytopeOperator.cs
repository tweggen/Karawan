using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Principal;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.world;
using static engine.Logger; 

namespace nogame.cities;

public class GeneratePolytopeOperator : IFragmentOperator
{
    private engine.world.ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;

    public string FragmentOperatorGetPath()
    {
        return $"8012/GeneratePolytopeOperator/{_myKey}/";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private async void _placePolytope(engine.world.Fragment worldFragment, engine.streets.Estate estate)
    {
        /*
         * We need to create two instances, one for the stand and one for the ball.
         * The stand will be static, the ball will not be, as it can be consumed.
         */
        Model model = await ModelCache.Instance().Instantiate(
            $"polytope-stand-only.obj", new builtin.loader.ModelProperties(), new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            //| InstantiateModelParams.ROTATE_Y180
                            ,
                MaxDistance = 500f
            });
        var vPos =
            _clusterDesc.Pos - worldFragment.Position +
            estate.GetCenter() with { Y = _clusterDesc.AverageHeight + 5.5f };
        worldFragment.AddStaticInstance(
            "nogame.furniture.polytopeStand", model.InstanceDesc,
                vPos, Quaternion.Identity, null);
        Trace($"Placing polytope @{worldFragment.Position+vPos}");
    }
    

    public Task FragmentOperatorApply(engine.world.Fragment worldFragment) => new Task(() =>
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        List<engine.streets.Estate> potentialEstates = new();
        
        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();


        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace("Skipping invalid quarter.");
                continue;
            }

            /*
             * Compute some properties of this quarter.
             * - is it convex?
             * - what is it extend?
             * - what is the largest side?
             */
            foreach (var estate in quarter.GetEstates())
            {
                /*
                 * Only consider this estate, if the center coordinate 
                 * is within this fragment.
                 */
                var center = estate.GetCenter();
                center.X += cx;
                center.Z += cz;
                if (!worldFragment.IsInsideLocal(center.X, center.Z))
                {
                    continue;
                }

                /*
                 * Polytope only can be done when no buildings are on top.
                 */
                var buildings = estate.GetBuildings();
                if (buildings.Count > 0)
                {
                    continue;
                }

                potentialEstates.Add(estate);
            }
        }

        int nEstates = potentialEstates.Count;
        if (0 == nEstates)
        {
            return;
        }

        int idx = (int)(_rnd.GetFloat() * nEstates);
        var polytopeEstate = potentialEstates[idx];
        _placePolytope(worldFragment, polytopeEstate);

    });

    public GeneratePolytopeOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);
    }
}