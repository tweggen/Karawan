using nogame.cities;
using System.Numerics;

namespace nogame.cubes
{
    internal class CubeBehavior : engine.IBehavior
    {
        engine.Engine _engine;
        engine.world.ClusterDesc _clusterDesc;
        engine.streets.StreetPoint _streetPoint;
        StreetNavigationController _snc;

        private static Vector3 _cubeHeight = new Vector3(0f, 4f, 0f);

        public void Behave(in DefaultEcs.Entity entity, float dt)
        {
            _snc.NavigatorBehave(dt);

            _engine.GetATransform().SetTransforms(
                entity,
                true, 0xffffffff,
                _snc.NavigatorGetOrientation(),
                _cubeHeight + _snc.NavigatorGetWorldPos()
            );
        }

        public CubeBehavior(
            in engine.Engine engine0,
            in engine.world.ClusterDesc clusterDesc0,
            in engine.streets.StreetPoint streetPoint0
            )
        {
            _engine = engine0;
            _clusterDesc = clusterDesc0;
            _streetPoint = streetPoint0;
            _snc = new StreetNavigationController(_clusterDesc, _streetPoint);
        }
    }
}
