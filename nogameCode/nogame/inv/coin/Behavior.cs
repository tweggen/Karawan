using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
 

namespace nogame.inv.coin;

public class Behavior : ABehavior
{
    static Quaternion _qRotateBy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 2f*Single.Pi/180f);

    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(in entity, dt);
        I.Get<TransformApi>().AppendRotation(entity, _qRotateBy);
    }
}