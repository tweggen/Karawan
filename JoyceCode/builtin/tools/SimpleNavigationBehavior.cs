using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.behave;
using engine.physics;
using static engine.Logger;
using static builtin.extensions.JsonObjectNumerics;

namespace builtin.tools;

public class SimpleNavigationBehavior : ABehavior
{
    INavigator _inav;
    private Quaternion _qPrevRotation = Quaternion.Identity;


    public INavigator Navigator
    {
        get => _inav;
        set => _inav = value;
    }
    
    
    public override void OnCollision(ContactEvent cev)
    {
    }

    
    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _inav.NavigatorBehave(dt);

        _inav.NavigatorGetTransformation(out var vPosition, out var qOrientation);

        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        engine.I.Get<engine.joyce.TransformApi>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            vPosition
        );
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
    }


    public override void OnAttach(in Engine engine0, in Entity entity)
    {
        base.OnAttach(engine0, entity);
        _inav.NavigatorLoad();
    }
}