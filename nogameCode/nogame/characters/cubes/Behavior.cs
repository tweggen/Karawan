using nogame.cities;
using System.Numerics;
using engine.physics;
using engine.world;

namespace nogame.characters.cubes
{
    internal class Behavior : engine.ABehavior
    {
        engine.Engine _engine;
        engine.world.ClusterDesc _clusterDesc;
        engine.streets.StreetPoint _streetPoint;
        StreetNavigationController _snc;


        public override void OnCollision(ContactEvent cev)
        {
        }


        public override void Behave(in DefaultEcs.Entity entity, float dt)
        {
            _snc.NavigatorBehave(dt);

            _snc.NavigatorGetTransformation(out var vPosition, out var qOrientation);
            engine.Implementations.Get<engine.transform.API>().SetTransforms(
                entity,
                true, 0x0000ffff,
                qOrientation, vPosition with
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
            _snc = new StreetNavigationController(_clusterDesc, _streetPoint);
            _snc.Speed = speed;
        }
    }
}
