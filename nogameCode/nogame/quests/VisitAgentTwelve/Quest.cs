using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce.components;
using engine.quest;
using engine.world.components;
using static engine.Logger;

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
        I.Get<engine.quest.Manager>().DeactivateQuest(this);
        I.Get<nogame.modules.story.Narration>().TriggerPath("agent12");
    }

    
    public string Name { get; set; }

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
        
    
        DefaultEcs.Entity eClosest = default;

        if (_engine.TryGetPlayerEntity(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
        {
            var v3Player = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
            foreach (var e in withIcon)
            {
                ref var cMapIcon = ref e.Get<MapIcon>();
                if (cMapIcon.Code != MapIcon.IconCode.Drink) continue;
                var v3D = (v3Player - e.Get<Transform3ToWorld>().Matrix.Translation);
                var d2d2 = (new Vector2(v3D.X, v3D.Z)).LengthSquared();
                if (d2d2 > 20f && d2d2 < mind2)
                {
                    if (e.IsAlive && e.IsEnabled())
                    {
                        eClosest = e;
                        mind2 = d2d2;
                    }
                }
            }
        }

        if (default == eClosest)
        {
            Error($"Unable to find any mark close to player entity.");           
        }

        /*
         * Create a destination marker.
         */
        {
            var v3Marker = eClosest.Get<Transform3ToWorld>().Matrix.Translation;
            var v3Target = v3Marker with
            {
                Y = I.Get<engine.world.MetaGen>().Loader.GetHeightAt(v3Marker) +
                    engine.world.MetaGen.ClusterNavigationHeight
            };

            _questTarget = new engine.quest.ToLocation()
            {
                RelativePosition = v3Target,
                SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
                SensitiveRadius = 10f,
                MapCameraMask = nogame.modules.map.Module.MapCameraMask,
                OnReachTarget = _onReachTarget
            };
            _questTarget.ModuleActivate();
        }
    }
}