using engine.behave;

namespace nogame.characters.citizen;


/**
 * The entity has been frightened and tries to escape from the threat.
 * It keeps fleeing until it is peaceful again.
 *
 * TXWTODO: I would need shared stats about the character at this point
 * to do it properly.
 *
 * TXWTODO: I would need to carry over the previous behavior's state
 * into this behavior. This is what sync was intended to.
 */
public class FleeStrategy : AEntityStrategyPart
{
    public override void OnEnter()
    {
        throw new System.NotImplementedException();
    }

    public override void OnExit()
    {
        throw new System.NotImplementedException();
    }
}