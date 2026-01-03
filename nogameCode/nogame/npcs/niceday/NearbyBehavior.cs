using engine;
using engine.news;

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