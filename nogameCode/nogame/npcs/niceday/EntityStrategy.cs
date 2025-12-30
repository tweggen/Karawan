using builtin.tools;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.news;
using engine.world;
using nogame.characters;
using static engine.Logger;

namespace nogame.npcs.niceday;


/**
 * Strategy for citizen entities.
 *
 * Uses two sub-strategies: WalkStrategy and RecoverStrategy.
 */
public class EntityStrategy : AOneOfStrategy
{
    private readonly RandomSource _rnd;
    
    private readonly PositionDescription _startPositionDescription;

    private readonly CharacterModelDescription _cmd;

    public CharacterState CharacterState { get; set; }
    
    public static string CrashHitEventPath(in DefaultEcs.Entity e) =>
        $"@{e.ToString()}/nogame.npcs.niceguy.onCrashHit";


    /**
     * If recover or flee gives up, we switch to recover.
     */
    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        // TXWTODO: Do it.
    }

    
    /**
     * Any character begins to walk.
     */
    public override string GetStartStrategy()
    {
        return "rest";
    }

    
    private EntityStrategy(RandomSource rnd,
        CharacterModelDescription cmd,
        PositionDescription pod,
        CharacterState chd)
    {
        _rnd = rnd;
        _cmd = cmd;
        _startPositionDescription = pod;
        CharacterState = chd;

        Strategies = new()
        {
            {
                "rest", new RestStrategy()
                {
                    Controller = this,
                    CharacterModelDescription = cmd, CharacterState = CharacterState,
                    PositionDescription = _startPositionDescription
                }
            },
        };
    }
    

    /**
      * Setup a strategy for a niceguy at the given position
      * with the given appearance. 
      */
    public static bool TryCreate(
        RandomSource rnd,
        PositionDescription pod,
        CharacterModelDescription cmd,
        out EntityStrategy entityStrategy)
    {
        /*
         * Now define, who we are, including, how fast we walk.
         */
        CharacterState chd = new();
        
        /*
         * Pass all that to the strategy ctor.
         */
        entityStrategy = new(rnd, cmd, pod, chd);
        
        return true;
    }
}