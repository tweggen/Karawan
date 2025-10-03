using System;
using System.Numerics;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.citizen;

public class Behavior : builtin.tools.SimpleNavigationBehavior
{
    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        var me = cev.ContactInfo.PropertiesA;
        _engine.AddDoomedEntity(me.Entity);
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
        base.Sync(entity);
    }
    

    public Behavior()
    {
    }

}