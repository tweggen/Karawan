using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.physics;

namespace nogame.cities;

public class ShopNearbyBehavior : ABehavior
{
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;
    
    public override void OnDetach(in Entity entity)
    {
        if (_eActionMarker.IsAlive)
        {
            _eActionMarker.Dispose();
        }
    }
    
    public override void OutOfRange(in Engine engine0, in Entity entity)
    {
        if (_eActionMarker.IsAlive)
        {
            _eActionMarker.Dispose();
        }
    }

    public override void InRange(in Engine engine0, in Entity entity)
    {
        _eActionMarker = engine0.CreateEntity("poi.shop.action");
        _eActionMarker.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 14f), 
            "E to enter", 18, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 100f });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        I.Get<TransformApi>().SetTransforms(_eActionMarker, true,
            0x00000001, Quaternion.Identity, Vector3.Zero);
    }
}