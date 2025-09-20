using nogame.cities;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.physics;
using engine.world;

namespace nogame.characters.cubes
{
    internal class Behavior : ABehavior
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
            engine.I.Get<engine.joyce.TransformApi>().SetTransforms(
                entity,
                true, 0x0000ffff,
                qOrientation, vPosition with
                {
                    Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight + 0.3f
                }
            );
        }


        public override void OnAttach(in Engine engine0, in Entity entity)
        {
            base.OnAttach(in engine0, in entity);
            _snc.NavigatorLoad();
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
            _snc = new StreetNavigationController()
            {
                ClusterDesc = _clusterDesc,
                StartPoint = _streetPoint
            };
            _snc.Speed = speed;
        }
    }
}
