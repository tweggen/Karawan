using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.news;
using nogame.modules.story;

namespace nogame.npcs.niceday;

public class NearbyBehavior : nogame.tools.ANearbyBehavior
{
    public override string Name { get => "nogame.npcs.niceday.talk"; }


    protected override void OnAction(Event ev)
    {
        ev.IsHandled = true;
        
        // TXWTODO: Trigger conversation.
        I.Get<nogame.modules.story.Narration>().TriggerConversation("niceguy", _eTarget.ToString());
    }
}