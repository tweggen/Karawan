using System;
using nogame.cities;
using System.Numerics;
using engine.world;

namespace nogame.characters.car3;

internal class Behavior : engine.IBehavior
{
    engine.Engine _engine;
    engine.world.ClusterDesc _clusterDesc;
    engine.streets.StreetPoint _streetPoint;
    StreetNavigationController _snc;
    private Quaternion _qPrevRotation = Quaternion.Identity;
    
    public void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);

        Quaternion qOrientation = _snc.NavigatorGetOrientation();
        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        _engine.GetATransform().SetTransforms(
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
