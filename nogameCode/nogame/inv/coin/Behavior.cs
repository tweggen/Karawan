using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
using engine.physics;


namespace nogame.inv.coin;


public class Behavior : ABehavior
{
    static Quaternion _qRotateBy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 2f*Single.Pi/180f);


    public override void OnCollision(ContactEvent cev)
    {
       I.Get<Engine>().AddDoomedEntity(cev.ContactInfo.PropertiesA.Entity);
    }
    
    
    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(in entity, dt);
        I.Get<TransformApi>().AppendRotation(entity, _qRotateBy);
    }
}