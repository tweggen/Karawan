using DefaultEcs;
using engine;
using engine.behave;
using engine.tale;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Behavior provider for TALE NPC entities. Handles Tier 2 → Tier 1 promotion
/// when the player re-enters range after an NPC was dematerialized.
/// </summary>
public class TaleEntityBehavior : ABehavior
{
    private TaleEntityStrategy _strategy;

    public TaleEntityBehavior(TaleEntityStrategy strategy)
    {
        _strategy = strategy;
    }

    public override void InRange(in Engine engine, in Entity entity)
    {
        // Player re-entered range: promote from Tier 2 to Tier 1
        if (_strategy._isTier2)
        {
            if (entity.Has<engine.tale.components.TaleNpcId>())
            {
                int npcId = entity.Get<engine.tale.components.TaleNpcId>().NpcId;
                _strategy.ExitTier2Mode();
                I.Get<TaleManager>().ClearTier2(npcId);
                Trace($"TALE BEHAVIOR: NPC {npcId} promoted from Tier 2 to Tier 1.");
            }
        }
    }

    public override void OutOfRange(in Engine engine, in Entity entity)
    {
        // Player left range: this will be handled by TerminateCharacters if
        // the fragment goes out of scope. Tier 2 demotion happens there.
    }
}
