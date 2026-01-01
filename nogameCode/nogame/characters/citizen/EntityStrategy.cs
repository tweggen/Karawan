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
    
    private readonly builtin.tools.SegmentNavigator _walkNavigator;

    private readonly PositionDescription _startPositionDescription;
    private PositionDescription _lastPositionDescription;

    private readonly CharacterModelDescription _cmd;
    public CharacterState CharacterState { get; set; }
    
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
        else if (
            strategy == Strategies["recover"]
            || strategy == Strategies["flee"])
        {
            _lastPositionDescription = _walkNavigator.Position;
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

    
    private SegmentNavigator? _createWalkNavigator()
    {
        /*
         * Create the route. The route always is around a quarter.
         */
        var segmentRoute = new QuarterLoopRouteGenerator()
        {
            ClusterDesc = _startPositionDescription.ClusterDesc, 
            Quarter = _startPositionDescription.Quarter
        }.GenerateRoute();

        /*
         * Create a segment navigator that will use the route.
         */
        SegmentNavigator navigator = new SegmentNavigator()
        {
            SegmentRoute = segmentRoute,
            Position = _startPositionDescription,
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
        CharacterModelDescription cmd,
        PositionDescription pod,
        CharacterState chd)
    {
        _rnd = rnd;
        _cmd = cmd;
        _startPositionDescription = pod;
        CharacterState = chd;

        _walkNavigator = _createWalkNavigator();

        Strategies = new()
        {
            {
                "flee", new FleeStrategy()
                {
                    Controller = this,
                    CharacterModelDescription = cmd, CharacterState = CharacterState,
                    Navigator = _walkNavigator
                }
            },
            {
                "recover", new RecoverStrategy()
                {
                    Controller = this,
                    CharacterModelDescription = cmd, CharacterState = CharacterState
                }
            },
            {
                "walk", new WalkStrategy()
                {
                    Controller = this,
                    CharacterModelDescription = cmd, CharacterState = CharacterState,
                    Navigator = _walkNavigator
                }
            }
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
         * Now define, who we are, including, how fast we walk.
         */
        CharacterState chd = new()
        {
            BasicSpeed = (4f + rnd.GetFloat() * 3f) / 3.6f,
        };
        
        /*
         * Pass all that to the strategy ctor.
         */
        entityStrategy = new(rnd, cmd, pod, chd);
        
        return true;
    }
}