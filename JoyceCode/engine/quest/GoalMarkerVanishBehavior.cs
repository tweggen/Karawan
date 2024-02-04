using System;
using System.Numerics;
using DefaultEcs;
using engine.behave;
using engine.joyce;

namespace engine.quest;

public class GoalMarkerVanishBehavior : ABehavior
{
    static Quaternion _qRotateBy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 2f*Single.Pi/180f);
    private static Vector3 _v3MoveUp = Vector3.UnitY * (3f / 60f);
    private static float _scaleDown = 0.95f;
    private static TransformApi _aTransformApi = I.Get<TransformApi>();
    
    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(in entity, dt);
        _aTransformApi.AppendRotation(entity, _qRotateBy);
        ref var t3 = ref _aTransformApi.GetTransform(entity);
        _aTransformApi.SetTransform(entity,
            Quaternion.Concatenate(t3.Rotation, _qRotateBy), 
            t3.Position + _v3MoveUp, 
            t3.Scale * _scaleDown);
    }
}