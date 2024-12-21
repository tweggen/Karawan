using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using engine.behave;
using engine.physics;
using static engine.Logger;
using static builtin.extensions.JsonObjectNumerics;

namespace builtin.tools;

public class SimpleNavigationBehavior : ABehavior
{
    engine.Engine _engine;
    INavigator _inav;
    private Quaternion _qPrevRotation = Quaternion.Identity;


    public INavigator Navigator
    {
        get => _inav;
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


    public override Func<Task> SetupFrom(JsonElement je) => new (async () =>
    {
        _qPrevRotation = ToQuaternion(je.GetProperty("sno").GetProperty("prevRotation"));
        _inav.SetupFrom(je);
    });

    
    public override void SaveTo(ref JsonObject jo)
    {
        JsonObject joSNO = new JsonObject();
        joSNO.Add("prevRotation", From(_qPrevRotation) );
        jo.Add("sno", joSNO);
        _inav.SaveTo(ref jo);
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