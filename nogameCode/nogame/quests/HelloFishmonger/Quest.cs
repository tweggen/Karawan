using System.Linq;
using engine;
using engine.behave.components;
using engine.quest;
using Silk.NET.Core.Native;
using static engine.Logger;

namespace nogame.quests.HelloFishmonger;

public class Quest : AModule, IQuest
{
    private bool _isActive = false;

    private engine.quest.TrailVehicle _questTarget;  
    
    private Description _description = new()
    {
        Title = "Trail the car.",
        ShortDescription = "The car quickly is departing. Follow it to its target!",
        LongDescription = "Isn't this a chase again?"
    };


    private void _onReachTarget()
    {
        I.Get<engine.quest.Manager>().DeactivateQuest(this);
        I.Get<nogame.modules.story.Narration>().TriggerPath("firstPubSecEncounter");
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
         * Randomly chose a destination car.
         */
        DefaultEcs.Entity eVictim = default;
        var behaving = _engine.GetEcsWorld().GetEntities()
            .With<engine.behave.components.Behavior>()
            .With<engine.joyce.components.Transform3ToWorld>()
            .AsEnumerable();

        foreach (var e in behaving)
        {
            if (!e.IsAlive) continue;
            ref var cBehavior = ref e.Get<Behavior>();
            if (cBehavior.Provider.GetType() != typeof(nogame.characters.car3.Behavior))
            {
                continue;
            }

            cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;
            eVictim = e;
            break;
        }

        if (eVictim == default)
        {
            Error($"No victim found after a lot of tries.");
            return;
        }

        _questTarget = new engine.quest.TrailVehicle()
        {
            SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            ParentEntity = eVictim,
            OnReachTarget = _onReachTarget
        };
        _questTarget.ModuleActivate();
    }
}