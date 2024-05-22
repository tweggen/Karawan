using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.physics;

namespace nogame.cities;

public class ShopNearbyBehavior : IBehavior
{
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;
    
    public void OnCollision(ContactEvent cev)
    {
        throw new System.NotImplementedException();
    }

    public void Behave(in Entity entity, float dt)
    {
        throw new System.NotImplementedException();
    }

    public void Sync(in Entity entity)
    {
    }

    public void OnDetach(in Entity entity)
    {
        _eActionMarker.Dispose();
    }

    public void OnAttach(in Engine engine0, in Entity entity)
    {
        _eActionMarker = engine0.CreateEntity("poi.shop.action");
        _eActionMarker.Set(new OSDText(new Vector2(-100f, 0f), new Vector2(200f, 14f), "E to enter", 14, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 100f });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        I.Get<TransformApi>().SetTransforms(_eActionMarker, true,
            0x00000001, Quaternion.Identity, Vector3.UnitY * (2.5f+2f));
    }
}