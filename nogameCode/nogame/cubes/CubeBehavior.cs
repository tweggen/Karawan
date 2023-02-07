using nogame.cities;
using System;
using System.Collections.Generic;
using System.Text;

namespace nogame.cubes
{
    internal class CubeBehavior : engine.IBehavior
    {
        engine.Engine _engine;
        engine.world.ClusterDesc _clusterDesc;
        engine.streets.StreetPoint _streetPoint;
        StreetNavigationController _snc;

        public void OnBehave(in DefaultEcs.Entity entity, float dt)
        {
            _snc.NavigatorBehave(dt);
            _engine.GetATransform().SetTransforms(
                entity,
                true, 0xffffffff,
                _snc.NavigatorGetOrientation(),
                _snc.NavigatorGetWorldPos()
            );
        }

        public CubeBehavior(
            in engine.Engine engine0,
            in engine.world.ClusterDesc clusterDesc0,
            in engine.streets.StreetPoint streetPoint0
            )
        {
            _clusterDesc = clusterDesc0;
            _streetPoint = streetPoint0;
            _snc = new StreetNavigationController(_clusterDesc, _streetPoint);
        }
    }
}
