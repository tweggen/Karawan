using System;
using System.Numerics;
using engine;
using engine.joyce.components;
using engine.quest;
using engine.world.components;

namespace nogame.quests.VisitAgentTwelve;

public class Quest : AModule, IQuest
{
    private bool _isActive = false;
    
    private engine.quest.ToLocation _questTarget;  
    
    private Description _description = new()
    {
        Title = "Come to the location.",
        ShortDescription = "Try to find the marker on the map and reach it.",
        LongDescription = "Every journey starts with the first step. Reach for the third step" +
                          " to make it an experience."
    };


    private void _onReachTarget()
    {
        // TXWTODO: Is this the proper way to advance the logic?
        I.Get<nogame.modules.story.Narration>().TriggerPath("agent12");
    }


    public Description GetDescription()
    {
        return _description;
    }

    
    public bool IsActive()
    {
        return _isActive;
    }


    public override void ModuleDeactivate()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;
        
        _engine.RemoveModule(this);
        _isActive = false;
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _isActive = true;
        _engine.AddModule(this);
        
        /*
         * Test code: Find any bar.
         * This is not smart. We just look for the one closest to the player.
         */
        var withIcon = 
            _engine.GetEcsWorld().GetEntities().With<Transform3ToWorld>().With<MapIcon>().AsEnumerable();
        float mind2 = Single.MaxValue;
        var v3Player = _engine.GetPlayerEntity().Get<Transform3ToWorld>().Matrix.Translation;
        DefaultEcs.Entity eClosest = default;
        foreach (var e in withIcon)
        {
            ref var cMapIcon = ref e.Get<MapIcon>();
            if (cMapIcon.Code != MapIcon.IconCode.Drink) continue;
            var d2 = (v3Player - e.Get<Transform3ToWorld>().Matrix.Translation).LengthSquared();
            if (d2 < mind2)
            {
                eClosest = e;
                mind2 = d2;
            }
        }
        
        /*
         * Test code to add a destination.
         */
        _questTarget = new engine.quest.ToLocation()
        {
            RelativePosition = eClosest.Get<Transform3ToWorld>().Matrix.Translation,
            SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
            SensitiveRadius = 15f,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            OnReachTarget = _onReachTarget
        };
        _questTarget.ModuleActivate();
    }
}