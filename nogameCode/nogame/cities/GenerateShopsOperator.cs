
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using builtin.tools;
using engine;
using engine.draw;
using engine.joyce;
using engine.world;
using engine.world.components;
using static engine.Logger;

namespace nogame.cities;

/**
 * Generate a standard set of shops.
 *
 * These shops create points of interest, which may be visible in a map
 * and may have a mission target later in a game.
 * They also may trigger an action if touched.
 * For visualization, they are associateed with a specific shopfront
 * on a building to make them stand out between other shops.
 */
class GenerateShopsOperator : IClusterOperator
{
    private RandomSource _rnd;
    

    /**
     * The map we shall render on
     */
    public uint MapCameraMask { get; set; } = 0x00800000;

    public bool TraceDetail { get; set; } = false;
    public bool TraceCreate { get; set; } = false;


    private void _createShops(ClusterDesc clusterDesc, float shopDistance, Func<Vector3, float> funcProbab, MapIcon.IconCode iconCode)
    {
        float shopArea = shopDistance*shopDistance;
        float clusterArea = clusterDesc.Size * clusterDesc.Size;
        int nTries = (int)(clusterArea / shopArea);

        HashSet<Vector3> setPositions = new();
        
        for (int t = 0; t < nTries; t++)
        {
            Vector2 v2TryLocal = new(
                    (_rnd.GetFloat()-0.5f) * clusterDesc.Size,
                    (_rnd.GetFloat()-0.5f) * clusterDesc.Size);
            Vector2 v2TryGlobal = v2TryLocal + clusterDesc.Pos2;

            float shopIntensity = funcProbab(new Vector3(v2TryGlobal.X, clusterDesc.AverageHeight, v2TryGlobal.Y));

            if (shopIntensity < 0.5f)
            {
                if (TraceDetail) Trace($"Discarding shop {t} outside of shopping zone.");
                continue;
            }

            var quarter = clusterDesc.GuessQuarter(v2TryGlobal);
            if (null == quarter)
            {
                if (TraceDetail) Trace($"Discarding shop {t} because null quarter.");
                continue;
            }
            
            /*
             * Just randomly pick an estate.
             */
            var estates = quarter.GetEstates();
            if (null == estates || 0 == estates.Count)
            {
                if (TraceDetail) Trace($"Discarding shop {t} because no estates.");
                continue;
            }

            var estate = estates[0];
            var buildings = estate.GetBuildings();
            if (null == buildings || 0 == buildings.Count)
            {
                if (TraceDetail) Trace($"Discarding shop {t} because no building.");
                continue;
            }
            
            /*
             * Just pick the first shop front.
             */
            var shopFronts = buildings[0].GetShopFronts();
            if (null == shopFronts || shopFronts.Count == 0)
            {
                if (TraceDetail) Trace($"Discarding shop {t} because no shop.");
                continue;
            }

            var myShopFront = shopFronts[0];
            var myShopFrontPoints = myShopFront.GetPoints();
            if (null == myShopFrontPoints || myShopFrontPoints.Count < 2)
            {
                if (TraceDetail) Trace($"Discarding shop {t} because no shop fronts.");
                continue;
            }
            var nShopFronts = myShopFrontPoints.Count;
            int shopIdx = (int)(_rnd.GetFloat() * nShopFronts);
            Vector3 v3ShopLocal = (myShopFrontPoints[(shopIdx)%nShopFronts] + myShopFrontPoints[(shopIdx+1)%nShopFronts]) / 2f;
            
            if (setPositions.Contains(v3ShopLocal))
            {
                if (TraceDetail) Trace($"Discarding shop at {v3ShopLocal} because it already exists.");
                continue;
            }

            setPositions.Add(v3ShopLocal);

            /*
             * Create POI right in the middle.
             */
            var e = I.Get<engine.Engine>();
            e.QueueEntitySetupAction("poi.shop", (DefaultEcs.Entity ePOI) =>
            {
                var v3ShopGlobal = (clusterDesc.Pos + v3ShopLocal with
                        {
                            Y = clusterDesc.AverageHeight + 30f + 5f * (t * 8f + (float)iconCode)
                        }
                    ); 
                if (TraceCreate) Trace($"Generating {iconCode} at {v3ShopGlobal}");
                I.Get<TransformApi>().SetTransforms(ePOI, true,
                    MapCameraMask, Quaternion.Identity, v3ShopGlobal);

                DefaultEcs.Entity eMapMarker = e.CreateEntity($"poi.shop map marker");
                I.Get<HierarchyApi>().SetParent(eMapMarker, ePOI); 
                I.Get<TransformApi>().SetTransforms(eMapMarker, true, 
                    MapCameraMask, Quaternion.Identity, Vector3.Zero);
                eMapMarker.Set(new engine.world.components.MapIcon() { Code = iconCode });
            });
        }
    }
    

    public void ClusterOperatorApply(ClusterDesc clusterDesc)
    {
        _rnd = new(clusterDesc.Name);

        
        Trace($"Creating fishmongers for {clusterDesc.Name}");
        /*
         * This is a stupidly simple imple100mentation, chosing just about one shop 100m100m of cluster size
         * in the shopping zone as a shop.
         *
         * We do a monte carlo approach. If the shops should be 100m apart on average, we
         * shoot area / (100*109m^2) times, applying a probability if we hit a shopping zone.
         */
        _createShops(
            clusterDesc,
            100f, 
            v3PosShop => clusterDesc.GetAttributeIntensity(
                v3PosShop, 
                ClusterDesc.LocationAttributes.Shopping),
            MapIcon.IconCode.Game2);

        Trace($"Creating bars for {clusterDesc.Name}");        
        _createShops(
            clusterDesc,
            50f, 
            v3PosShop => clusterDesc.GetAttributeIntensity(
                v3PosShop,  
                ClusterDesc.LocationAttributes.Downtown),
            MapIcon.IconCode.Drink);

        Trace($"Creating takeaways for {clusterDesc.Name}");        
        _createShops(
            clusterDesc,
            130f, 
            v3PosShop => 1f,
            MapIcon.IconCode.Eat);

    }
}