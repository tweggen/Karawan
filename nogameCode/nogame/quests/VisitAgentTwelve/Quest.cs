using System.Numerics;
using engine;
using engine.quest;

namespace nogame.quests.VisitAgentTwelve;

public class Quest : AModule, IQuest
{
    private bool _isActive = false;
    
    private Description _description = new()
    {
        Title = "Come to the location.",
        ShortDescription = "Try to find the marker on the map and reach it.",
        LongDescription = "Every journey starts with the first step. Reach for the third step" +
                          " to make it an experience."
    };


    private void _onReachTarget()
    {
        // TXWTODO: Advance the story.
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
        // TXWTODO: Create the quest id as a marker for deletion.
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
         * Test code to add a destination.
         */
        var newQuestTarget = new engine.quest.ToLocation()
        {
            RelativePosition = new Vector3(-440f, 40f, 389f),
            SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            OnReachTarget = _onReachTarget
        };
        newQuestTarget.OperatorApply();
    }
}