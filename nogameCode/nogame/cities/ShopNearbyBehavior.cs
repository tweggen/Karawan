using System;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.draw.components;
using engine.joyce;
using engine.news;
using nogame.tools;

namespace nogame.cities;

public class ShopNearbyBehavior : ANearbyBehavior
{
    public override string Name { get => "nogame.modules.shop.open"; }

    protected override void OnAction(Event ev)
    {
        I.Get<nogame.modules.shop.Module>().ModuleActivate();
        ev.IsHandled = true;
    }
}