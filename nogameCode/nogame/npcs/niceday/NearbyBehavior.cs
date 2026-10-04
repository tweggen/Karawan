using DefaultEcs;
using engine;
using engine.news;
using nogame.characters;
using nogame.characters.citizen;

namespace nogame.npcs.niceday;

public class NearbyBehavior : nogame.tools.ANearbyBehavior
{
    public override string Name { get => "nogame.npcs.niceday.talk"; }

    public override string Prompt { get => "E to Talk"; }

    /**
     * Which character this is, so that its idle clip can be re-issued until it takes.
     *
     * This is the whole of a niceday NPC's animation, and until now there was none. The
     * strategy starts in "rest", RestStrategy.OnEnter attaches this behaviour, and this
     * behaviour drove the "E to Talk" prompt and nothing else - so the character's entire
     * animation was EntityCreator.InitialAnimName, a single unretried call issued before
     * FromModel had necessarily arrived. That is the exact shape that made the first T-pose
     * fix a no-op, and "the creation site names a driver" was true of this site throughout.
     */
    public CharacterModelDescription CharacterModelDescription = null;

    private AnimationDriver _driver;


    protected override void OnAction(Event ev)
    {
        ev.IsHandled = true;

        // TXWTODO: Trigger conversation.
        I.Get<nogame.modules.story.Narration>().TriggerConversation("niceguy", _eTarget.ToString());
    }


    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        _driver.Drive(entity, nameof(NearbyBehavior),
            CharacterModelDescription?.IdleAnimName);
    }


    public override void OnAttach(in Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        _driver.Reset();
    }
}
