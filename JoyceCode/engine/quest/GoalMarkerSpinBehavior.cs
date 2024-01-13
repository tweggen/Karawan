using System;
using System.Numerics;
using DefaultEcs;
using engine.behave;
using engine.joyce;
using engine.joyce.components;

namespace engine.quest;

public class GoalMarkerSpinBehavior : ABehavior
{
    static Quaternion _qRotateBy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 5f*Single.Pi/180f);

    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(in entity, dt);
        I.Get<TransformApi>().AppendRotation(entity, _qRotateBy);
    }
}