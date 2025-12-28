using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.news;
using engine.streets;
using engine.world;

namespace nogame.characters.citizen;


/**
 * Strategy for citizen entities.
 *
 * Uses two sub-strategies: WalkStrategy and RecoverStrategy.
 */
public class EntityStrategy : AOneOfStrategy
{
    private readonly RandomSource _rnd;
    private readonly ClusterDesc _clusterDesc;
    private readonly CharacterModelDescription _cmd;
    
    private readonly builtin.tools.SegmentNavigator _walkNavigator;

    private readonly PositionDescription _startPositionDescription;
    
    
    /**
     * The path used by sub-ordinate behaviors to publish
     * and by sub-ordinate strategies to subscribe to a collision.
     */
    static public string CrashEventPath(in DefaultEcs.Entity e) =>
        $"@{e.ToString()}/nogame.characters.citizen.onCrash";
    
    
    /**
     * If strategy walk gives up, we switch to recover.
     */
    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["walk"])
        {
            TriggerStrategy("recover");
        }
        else if (strategy == Strategies["recover"])
        {
            TriggerStrategy("walk");
        }
    }

    
    /**
     * Any character begins to walk.
     */
    public override string GetStartStrategy()
    {
        return "walk";
    }

    
    #region IEntityStrategy
    /*
     * nothing to override
     */
    #endregion


    private static SegmentNavigator? _createWalkNavigator(
        RandomSource rnd,
        CharacterModelDescription cmd,
        PositionDescription startPositionDescription)
    {
        /*
         * Create the route. The route always is around a quarter.
         */
        var segmentRoute = new QuarterLoopRouteGenerator()
        {
            ClusterDesc = startPositionDescription.ClusterDesc, 
            Quarter = startPositionDescription.Quarter
        }.GenerateRoute();

        /*
         * This is the default speed.
         */
        float speed;
        speed = (4f + rnd.GetFloat() * 3f) / 3.6f;

        /*
         * Create a segment navigator that will use the route.
         */
        SegmentNavigator navigator = new SegmentNavigator()
        {
            SegmentRoute = segmentRoute,
            Position = startPositionDescription,
            Speed = speed
        };
        
        return navigator;
    }


    private EntityStrategy(RandomSource rnd,
        ClusterDesc clusterDesc,
        CharacterModelDescription cmd,
        PositionDescription pod)
    {
        _rnd = rnd;
        _clusterDesc = clusterDesc;
        _cmd = cmd;
        _startPositionDescription = pod;

        _walkNavigator = _createWalkNavigator(_rnd, _cmd, _startPositionDescription);

        
        Strategies = new()
        {
            { "walk", new WalkStrategy() { Controller = this, CharacterModelDescription = cmd, Navigator =  _walkNavigator } },
            { "recover", new RecoverStrategy() { Controller = this, CharacterModelDescription = cmd } }
        };
    }


    static public void _chooseStartPosition(
        builtin.tools.RandomSource rnd, Fragment worldFragment, ClusterDesc clusterDesc,
        out PositionDescription pod)
    {
        PlacementContext pc = new()
        {
            CurrentFragment = worldFragment,
            CurrentCluster = clusterDesc
        };

        PlacementDescription plad = new()
        {
            ReferenceObject = PlacementDescription.Reference.StreetPoint,
            WhichFragment = PlacementDescription.FragmentSelection.CurrentFragment,
            WhichCluster = PlacementDescription.ClusterSelection.CurrentCluster,
            WhichQuarter = PlacementDescription.QuarterSelection.AnyQuarter
        };
        
        bool isPlaced = I.Get<Placer>().TryPlacing(rnd, pc, plad, out pod);
        if (!isPlaced) return;
        pod.RelativePos = rnd.GetFloat();
    }
    

    
    /**
      * Factory method to try to establish this strategy.
      */
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
        _chooseStartPosition(rnd, worldFragment, clusterDesc, out var pod);
        if (pod == null)
        {
            entityStrategy = null;
            return false;
        }

        /*
         * Pass all that to the strategy ctor.
         * TXWTODO: Remove all the fuzz from the strategy ctor and factor into a state.
         */
        entityStrategy = new(rnd, clusterDesc, cmd, pod);
        return true;
    }
}