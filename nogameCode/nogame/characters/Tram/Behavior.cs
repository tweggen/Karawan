using System;
using nogame.cities;
using System.Numerics;
using engine.world;

namespace nogame.characters.tram;

internal class Behavior : engine.IBehavior
{
    private readonly engine.Engine _engine;
    private readonly engine.world.ClusterDesc _clusterDesc;
    private readonly StreetNavigationController _snc;
    
    private engine.streets.StreetPoint _streetPoint;
    private Quaternion _qPrevRotation = Quaternion.Identity;
    private float _height;


    public void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);

        Quaternion qOrientation = _snc.NavigatorGetOrientation();
        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        Vector3 worldPos = _snc.NavigatorGetWorldPos();

        engine.Implementations.Get<engine.transform.API>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            worldPos with
            {
                Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight + _height
            }
        );
    }

    public Behavior SetSpeed(float speed)
    {
        _snc.NavigatorSetSpeed(speed);
        return this;
    }
    
    public Behavior SetHeight(float height)
    {
        _height = height;
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