using System;
using nogame.cities;
using System.Numerics;
using engine.physics;
using engine.world;

namespace nogame.characters.car3;

internal class Behavior : engine.IBehavior
{
    engine.Engine _engine;
    engine.world.ClusterDesc _clusterDesc;
    engine.streets.StreetPoint _streetPoint;
    StreetNavigationController _snc;
    private Quaternion _qPrevRotation = Quaternion.Identity;


    public void OnCollision(ContactEvent cev)
    {
        // throw new NotImplementedException();
    }

    public void Sync(in DefaultEcs.Entity entity)
    {
        if (entity.Has<engine.physics.components.Kinetic>())
        {
            var prefTarget = entity.Get<engine.physics.components.Kinetic>().Reference;
            Vector3 vPos3 = prefTarget.Pose.Position;
            Quaternion qRotation = prefTarget.Pose.Orientation;
            _snc.TakeCurrentPosition(vPos3, qRotation);
        }

    }


    public void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);

        Quaternion qOrientation = _snc.NavigatorGetOrientation();
        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        engine.Implementations.Get<engine.transform.API>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            _snc.NavigatorGetWorldPos() with
            {
                Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight
            }
        );
    }

    public Behavior SetSpeed(float speed)
    {
        _snc.NavigatorSetSpeed(speed);
        return this;
    }
    
    
    public Behavior(
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
