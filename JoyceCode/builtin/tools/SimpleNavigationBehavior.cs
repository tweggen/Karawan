using System.Numerics;
using engine;
using engine.behave.components;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace builtin.tools;

public class SimpleNavigationBehavior : engine.ABehavior
{
    engine.Engine _engine;
    engine.world.ClusterDesc _clusterDesc;
    engine.streets.StreetPoint _streetPoint;
    INavigator _inav;
    private Quaternion _qPrevRotation = Quaternion.Identity;


    public INavigator Navigator
    {
        get => _inav;
    }
    
    public override void OnCollision(ContactEvent cev)
    {
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
    }


    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _inav.NavigatorBehave(dt);

        _inav.NavigatorGetTransformation(out var vPosition, out var qOrientation);

        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        engine.I.Get<engine.transform.API>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            vPosition
        );
    }

    
    public SimpleNavigationBehavior(
        in engine.Engine engine0,
        in INavigator inav
    )
    {
        _engine = engine0;
        _inav = inav;
    }
}