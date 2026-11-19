
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using builtin.tools;
using engine;
using engine.draw;
using engine.draw.components;
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
        var placed = ShopPlacement.Place(clusterDesc, clusterDesc.QuarterStore(), ctx.Rnd,
            shopDistance, funcProbab, iconCode, TraceDetail);
        foreach (var shop in placed)
        {
            var quarter = shop.Quarter;
            var shopBuilding = shop.Building;
            var v3ShopLocal = shop.Local;
            int t = shop.Try;

            /*
             * Create the POI right in the middle of the shopfront ShopPlacement chose.
             */
            var e = I.Get<engine.Engine>();

            /*
             * The interaction point goes on the SAME storey as the window it belongs
             * to. It used to be the only thing on a block that did not ask the quarter
             * at all - it sampled ClusterDesc.GroundHeightAt, i.e. the TERRAIN, which
             * on a hillside city is neither the block floor nor the shop - and
             * ShopNearbyBehavior scores in 3-D with a radius of 16 m, so a shop whose
             * window and whose interaction point are a storey apart is a shop that
             * cannot be entered from in front of its own door.
             *
             * Read here rather than inside the queued action: this is generation-time
             * geometry, and the two must not be able to answer differently.
             */
            float shopGroundY = engine.streets.generation.BuildingFooting.StoreyGroundAt(
                quarter, shopBuilding, new Vector2(v3ShopLocal.X, v3ShopLocal.Z));

            e.QueueEntitySetupAction("poi.shop", (DefaultEcs.Entity ePOI) =>
            {
                var v3ShopGlobal = (clusterDesc.Pos + v3ShopLocal) with
                {
                    Y = shopGroundY + 2.5f + 1f
                };
                if (TraceCreate) Trace($"Generating {iconCode} at {v3ShopGlobal}");
                
                I.Get<TransformApi>().SetTransforms(ePOI, false,
                    MapCameraMask, Quaternion.Identity, v3ShopGlobal);
                var shopNearbyBehavior = new ShopNearbyBehavior() {
                    EPOI = ePOI
                };
                ePOI.Set(new engine.behave.components.Behavior(shopNearbyBehavior)
                {
                    Flags = (ushort)engine.behave.components.Behavior.BehaviorFlags.DontVisibInRange,
                    MaxDistance = (short)shopNearbyBehavior.Distance
                });
                
                DefaultEcs.Entity eMapMarker = e.CreateEntity($"poi.shop map marker");
                I.Get<HierarchyApi>().SetParent(eMapMarker, ePOI);
                I.Get<TransformApi>().SetTransforms(eMapMarker, true,
                    MapCameraMask, Quaternion.Identity, Vector3.UnitY * (25f + (t + 5f*(float)iconCode)));
                eMapMarker.Set(new engine.world.components.MapIcon() { Code = iconCode });
            });
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
