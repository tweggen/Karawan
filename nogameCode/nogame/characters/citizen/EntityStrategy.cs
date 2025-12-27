using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.streets;
using engine.world;

namespace nogame.characters.citizen;


/**
 * Strategy for citizen entities.
 *
 * Creates and owns the citizen's behavior.
 */
public class EntityStrategy : IEntityStrategy
{
    private OneOfStrategy _strategyController = new();

    private readonly RandomSource _rnd;
    private readonly ClusterDesc _clusterDesc;
    private readonly CharacterModelDescription _cmd;
        
    private Behavior _behavior;
    private readonly Quarter _quarter;
    private readonly QuarterDelim _delim;
    private readonly float _relativePos;

    
    public void Sync(in Entity entity)
    {
        _behavior.Sync(entity);
    }

    
    public void OnDetach(in Entity entity)
    {
        entity.Remove<engine.behave.components.Behavior>();
    }

    
    public void OnAttach(in Engine engine0, in Entity entity)
    {
        entity.Set(new engine.behave.components.Behavior(_behavior));
    }

    
    private static Behavior? _createDefaultBehavior(
        RandomSource rnd,
        ClusterDesc clusterDesc,
        CharacterModelDescription cmd,
        Quarter quarter, QuarterDelim delim, float relativePos)
    {
        /*
         * Create the route. The route always is around a quarter.
         */
        var segmentRoute = new QuarterRouteGenerator()
        {
            ClusterDesc = clusterDesc, Quarter = quarter, QuarterDelim = delim
        }.GenerateRoute();

        /*
         * This is the default speed.
         */
        float speed;
        speed = (4f + rnd.GetFloat() * 3f) / 3.6f;

        /*
         * Create a segment navigator that will use the route.
         */
        builtin.tools.SegmentNavigator segnav = new()
        {
            SegmentRoute = segmentRoute,
            Speed = speed
        };

        /*
         * Now create the behavior using that very navigator.
         */
        return new nogame.characters.citizen.Behavior()
        {
            Navigator = segnav,
            CharacterModelDescription = cmd
        };
    }


    private EntityStrategy(RandomSource rnd,
        ClusterDesc clusterDesc,
        CharacterModelDescription cmd,
        Quarter quarter, QuarterDelim delim, float relativePos)
    {
        _rnd = rnd;
        _clusterDesc = clusterDesc;
        _cmd = cmd;
        _quarter = quarter;
        _delim = delim;
        _relativePos = relativePos;

        
        _strategyController = new()
        {
            Strategies = new()
            {
                { "walk", new WalkStrategy() },
                { "recover", new RecoverStrategy() }
            }
        };

        
        _behavior = _createDefaultBehavior(rnd, clusterDesc, cmd,
            quarter, delim, relativePos);
    }

    
    public static bool TryCreate(
        RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        CharacterModelDescription cmd,
        out EntityStrategy entityStrategy)
    {
        /*
         * Try to come up with an entity strategy for an entity at that location.
         */
        CharacterCreator.ChooseQuarterDelimPointPos(rnd, worldFragment, clusterDesc,
            out var quarter, out var delim, out var relativePos);
        if (quarter == null)
        {
            entityStrategy = null;
            return false;
        }

        /*
         * Pass all that to the strategy ctor.
         * TXWTODO: Remove all the fuzz from the strategy ctor and factor into a state.
         */
        entityStrategy = new(rnd, clusterDesc, cmd, quarter, delim, relativePos);
        return true;
    }
}