using builtin.tools;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.news;
using engine.world;
using static engine.Logger;

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
    private PositionDescription _lastPositionDescription;
    
    /**
     * The path used by sub-ordinate behaviors to publish
     * and by sub-ordinate strategies to subscribe to a collision.
     */
    static public string CrashEventPath(in DefaultEcs.Entity e) =>
        $"@{e.ToString()}/nogame.characters.citizen.onCrash";
    
    static public string HitEventPath(in DefaultEcs.Entity e) =>
        $"@{e.ToString()}/nogame.characters.citizen.onHit";


    private void _onCrashEvent(Event ev)
    {
        /*
         * If we are not already in recover, transition to recover.
         */
        TriggerStrategy("recover");
    }


    private void _onHitEvent(Event ev)
    {
        /*
         * If we are not already in flee, trigger flee.
         */
        TriggerStrategy("flee");
    }
    
    
    /**
     * If recover or flee gives up, we switch to recover.
     */
    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["walk"])
        {
            Warning("We should not receive a give up walk");
        }
        else if (strategy == Strategies["recover"])
        {
            TriggerStrategy("walk");
            _walkNavigator.Position = _lastPositionDescription;
        } 
        {
            TriggerStrategy("walk");
            _walkNavigator.Position = _lastPositionDescription;
        }
    }

    
    /**
     * Any character begins to walk.
     */
    public override string GetStartStrategy()
    {
        return "walk";
    }

    
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


    #region IStrategyPart
    
    /**
     * Remove walk behavior and clean up.
     */
    public override void OnExit()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        sm.Unsubscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);
        base.OnExit();
    }

    
    /**
     * Add walk behavior and initialize.
     */
    public override void OnEnter()
    {
        base.OnEnter();
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        sm.Subscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);
    }
    
    #endregion
    
    
    #region IEntityStrategy
    /*
     * Nothing to override
     */
    #endregion

    
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
            { "flee", new FleeStrategy() { Controller = this, CharacterModelDescription = cmd, Navigator =  _walkNavigator } },
            { "recover", new RecoverStrategy() { Controller = this, CharacterModelDescription = cmd } },
            { "walk", new WalkStrategy() { Controller = this, CharacterModelDescription = cmd, Navigator =  _walkNavigator } }
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
        pod.QuarterDelimPos = rnd.GetFloat();
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