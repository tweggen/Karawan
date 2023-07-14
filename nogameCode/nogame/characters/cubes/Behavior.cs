using nogame.cities;
using System.Numerics;
using engine.world;

namespace nogame.characters.cubes
{
    internal class Behavior : engine.IBehavior
    {
        engine.Engine _engine;
        engine.world.ClusterDesc _clusterDesc;
        engine.streets.StreetPoint _streetPoint;
        StreetNavigationController _snc;
        
        
        public void Behave(in DefaultEcs.Entity entity, float dt)
        {
            _snc.NavigatorBehave(dt);

            _engine.GetATransform().SetTransforms(
                entity,
                true, 0x0000ffff,
                _snc.NavigatorGetOrientation(),
                _snc.NavigatorGetWorldPos() with
                {
                    Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight + 1f
                }
            );
        }
        

        public Behavior(
            in engine.Engine engine0,
            in engine.world.ClusterDesc clusterDesc0,
            in engine.streets.StreetPoint streetPoint0,
            in float speed
            )
        {
            _engine = engine0;
            _clusterDesc = clusterDesc0;
            _streetPoint = streetPoint0;
            _snc = new StreetNavigationController(_clusterDesc, _streetPoint).NavigatorSetSpeed(speed);
        }
    }
}
