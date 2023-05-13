using System;
using nogame.cities;
using System.Numerics;

namespace nogame.characters.Car3;

internal class Car3Behavior : engine.IBehavior
{
    engine.Engine _engine;
    engine.world.ClusterDesc _clusterDesc;
    engine.streets.StreetPoint _streetPoint;
    StreetNavigationController _snc;
    private Quaternion _qRotY180;
    private Quaternion _qPrevRotation = Quaternion.Identity;

    private static Vector3 _car3Height = new Vector3(0f, 3f, 0f);

    public void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);

        Quaternion qOrientation = _snc.NavigatorGetOrientation() * _qRotY180;
        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        _engine.GetATransform().SetTransforms(
            entity,
            true, 0xffffffff,
            qOrientation,
            _car3Height + _snc.NavigatorGetWorldPos()
        );
    }

    public Car3Behavior SetSpeed(float speed)
    {
        _snc.NavigatorSetSpeed(speed);
        return this;
    }
    
    public Car3Behavior(
        in engine.Engine engine0,
        in engine.world.ClusterDesc clusterDesc0,
        in engine.streets.StreetPoint streetPoint0
    )
    {
        _engine = engine0;
        _clusterDesc = clusterDesc0;
        _streetPoint = streetPoint0;
        _snc = new StreetNavigationController(_clusterDesc, _streetPoint);
        _qRotY180 = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), (float)Math.PI);
    }
}
