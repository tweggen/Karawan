
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
    private class Context
    {
        public ClusterDesc ClusterDesc;
        public RandomSource Rnd;
    }

    /**
     * The map we shall render on
     */
    public uint MapCameraMask { get; set; } = 0x00800000;

    public bool TraceDetail { get; set; } = false;
    public bool TraceCreate { get; set; } = false;


    private void _createShops(Context ctx, float shopDistance, Func<Vector3, float> funcProbab, MapIcon.IconCode iconCode)
    {
        var clusterDesc = ctx.ClusterDesc;
        float shopArea = shopDistance*shopDistance;
        float clusterArea = ctx.ClusterDesc.Size * ctx.ClusterDesc.Size;
        int nTries = (int)(clusterArea / shopArea);

        HashSet<Vector3> setPositions = new();
        
        for (int t = 0; t < nTries; t++)
        {
            bool isBadTry = false; 
            Vector2 v2TryLocal = new(
                    (ctx.Rnd.GetFloat()-0.5f) * clusterDesc.Size,
                    (ctx.Rnd.GetFloat()-0.5f) * clusterDesc.Size);
            Vector2 v2TryGlobal = v2TryLocal + ctx.ClusterDesc.Pos2;

            float shopIntensity = funcProbab(new Vector3(v2TryGlobal.X, clusterDesc.AverageHeight, v2TryGlobal.Y));

            if (shopIntensity < 0.5f)
            {
                isBadTry = true;
                if (TraceDetail) Trace($"Discarding shop {t} outside of shopping zone.");
                continue;
            }

            var quarter = clusterDesc.GuessQuarter(v2TryGlobal);
            if (null == quarter)
            {
                isBadTry = true;
                if (TraceDetail) Trace($"Discarding shop {t} because null quarter.");
                continue;
            }
            
            /*
             * Just randomly pick an estate.
             */
            var estates = quarter.GetEstates();
            if (null == estates || 0 == estates.Count)
            {
                isBadTry = true;
                if (TraceDetail) Trace($"Discarding shop {t} because no estates.");
                continue;
            }

            int nEstates = estates.Count;
            int estateStartIdx = (int)(ctx.Rnd.GetFloat() * nEstates);
            IList<engine.streets.Building> buildings = null; 
            for (int estateOfs = 0; estateOfs < nEstates; estateOfs++)
            {
                int estateIdx = (estateStartIdx + estateOfs) % nEstates;
                engine.streets.Estate myEstate = estates[estateIdx];
                buildings = myEstate.GetBuildings();
                if (null != buildings && buildings.Count > 0)
                {
                    break;
                }
            }
            
            if (null == buildings || buildings.Count == 0)
            {
                isBadTry = true;
                if (TraceDetail) Trace($"Discarding shop {t} because no building.");
                continue;
            }
            
            /*
             * TXWTODO: Iterate through the building's shop fronts until we found the first one
             * to use.
             *
             * ... we right now just take the first one.
             */
            int nBuildings = buildings.Count;
            int buildingStartIdx = (int)(ctx.Rnd.GetFloat() * nBuildings);
            IList<engine.streets.ShopFront> shopFronts = null;
            for (int buildingOfs = 0; buildingOfs < nBuildings; buildingOfs++)
            {
                int buildingIdx = (buildingStartIdx + buildingOfs) % nBuildings;
                engine.streets.Building myBuilding = buildings[buildingIdx];
                shopFronts = myBuilding.GetShopFronts();
                if (null != shopFronts && shopFronts.Count > 0)
                {
                    break;
                }
            }

            if (null == shopFronts || shopFronts.Count == 0)
            {
                isBadTry = false;
                if (TraceDetail) Trace($"Discarding shop {t} because no shop.");
                continue;
            }

            int nShops = shopFronts.Count;
            int shopStartIdx = (int)(ctx.Rnd.GetFloat() * nShops);
            for (int shopOfs=0; shopOfs<nShops; shopOfs++)
            {
                int shopIdx = (shopStartIdx + shopOfs) % nShops;
                var myShopFront = shopFronts[shopIdx];
                if (myShopFront.Tags.Contains("shop"))
                {
                    // no bad try.
                    if (TraceDetail) Trace($"Not using {t} because shopFront already tagged shop, trying to find another one for this try.");
                    continue;
                }

                /*
                 * Tag the new shop.
                 */
                // TXWTODO: This is a wrong tag, and no meaningful set of tags either.
                string tagShop = $"shop {iconCode}";
                myShopFront.Tags.Add(tagShop);

                // TXWTODO:Remove the actual creation of  entities from this.
                var myShopFrontPoints = myShopFront.GetPoints();
                if (null == myShopFrontPoints || myShopFrontPoints.Count < 2)
                {
                    // no bad try.
                    if (TraceDetail) Trace($"Discarding shop {t} because no shop front points.");
                    continue;
                }

                var nShopFronts = myShopFrontPoints.Count;
                int shopFrontIdx = (int)(ctx.Rnd.GetFloat() * nShopFronts);
                Vector3 v3ShopLocal = (myShopFrontPoints[(shopFrontIdx) % nShopFronts] +
                                       myShopFrontPoints[(shopFrontIdx + 1) % nShopFronts]) / 2f;

                if (setPositions.Contains(v3ShopLocal))
                {
                    // no bad try.
                    if (TraceDetail) Trace($"Discarding shop at {v3ShopLocal} because it already exists.");
                    continue;
                }

                setPositions.Add(v3ShopLocal);

                // TXWTODO: We shouldn't create POIs right here, should we?
                /*
                 * Create POI right in the middle.
                 */
                var e = I.Get<engine.Engine>();
                e.QueueEntitySetupAction("poi.shop", (DefaultEcs.Entity ePOI) =>
                {
                    var v3ShopGlobal = (clusterDesc.Pos + v3ShopLocal with
                            {
                                Y = clusterDesc.AverageHeight + 30f + (t + 5f*(float)iconCode)
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
                break;
            }
            
            // We placed a shop if isBadTry==false
        }
    }
    

    public void ClusterOperatorApply(ClusterDesc clusterDesc)
    {
        var ctx = new Context()
        {
            ClusterDesc = clusterDesc,
            Rnd = new(clusterDesc.Name)
        };

        
        Trace($"Creating fishmongers for {clusterDesc.Name}");
        
        /*
         * This is a stupidly simple imple100mentation, chosing just about one shop 100m100m of cluster size
         * in the shopping zone as a shop.
         *
         * We do a monte carlo approach. If the shops should be 100m apart on average, we
         * shoot area / (100*109m^2) times, applying a probability if we hit a shopping zone.
         */
        _createShops(
            ctx,
            100f, 
            v3PosShop => clusterDesc.GetAttributeIntensity(
                v3PosShop, 
                ClusterDesc.LocationAttributes.Shopping),
            MapIcon.IconCode.Game2);

        Trace($"Creating bars for {clusterDesc.Name}");        
        _createShops(
            ctx,
            100f, 
            v3PosShop => clusterDesc.GetAttributeIntensity(
                v3PosShop,  
                ClusterDesc.LocationAttributes.Downtown),
            MapIcon.IconCode.Drink);

        Trace($"Creating takeaways for {clusterDesc.Name}");        
        _createShops(
            ctx,
            100f, 
            v3PosShop => 1f,
            MapIcon.IconCode.Eat);

    }
}
