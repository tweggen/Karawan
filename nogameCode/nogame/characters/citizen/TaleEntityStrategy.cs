using System;
using System.Numerics;
using builtin.tools;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.navigation;
using engine.news;
using engine.tale;
using engine.world;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Strategy for TALE-driven NPC entities.
/// Transitions: advance storylet → travel to destination → activity at location → repeat.
/// Also handles flee/recover from collisions (same as citizen EntityStrategy).
/// </summary>
public class TaleEntityStrategy : AOneOfStrategy
{
    private readonly int _npcId;
    private readonly TaleManager _taleManager;
    private readonly CharacterModelDescription _cmd;
    private readonly CharacterState _characterState;
    private readonly PositionDescription _startPosition;
    private PositionDescription _currentPosition;

    private readonly GoToStrategyPart _goTo;
    private readonly StayAtStrategyPart _stayAt;
    private RoutingPreferences _routingPreferences;
    private DateTime _lastPreferenceUpdate;

    /// <summary>
    /// Seconds of real time per game day. Used to convert game-time
    /// storylet durations to real-time StayAt durations.
    /// </summary>
    public float RealSecondsPerGameDay { get; set; } = 30f * 60f;

    /// <summary>
    /// Get current routing preferences for this NPC.
    /// </summary>
    public RoutingPreferences RoutingPreferences => _routingPreferences;


    private void _onCrashEvent(Event ev)
    {
        TriggerStrategy("recover");
    }


    private void _onHitEvent(Event ev)
    {
        var active = GetActiveStrategy();
        if (active != Strategies["recover"])
        {
            TriggerStrategy("flee");
        }
    }


    public override void GiveUpStrategy(IStrategyPart strategy)
    {
        if (strategy == Strategies["travel"])
        {
            // Travel complete → start activity at destination
            _setupActivity();
            TriggerStrategy("activity");
        }
        else if (strategy == Strategies["activity"])
        {
            // Activity complete → advance to next storylet
            _advanceAndTravel();
        }
        else if (strategy == Strategies["flee"] || strategy == Strategies["recover"])
        {
            // After flee/recover → resume activity or travel
            TriggerStrategy("activity");
        }
    }


    public override string GetStartStrategy()
    {
        return "activity";
    }


    /// <summary>
    /// Update routing preferences based on current schedule and state.
    /// </summary>
    private void UpdateRoutingPreferences(DateTime currentTime)
    {
        // Only update periodically (not every frame)
        if ((currentTime - _lastPreferenceUpdate).TotalSeconds < 10.0)
            return;

        _lastPreferenceUpdate = currentTime;

        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null)
            return;

        // Determine goal based on schedule
        if (schedule.IsLate)
        {
            _routingPreferences.Goal = NpcGoal.OnTime;
            _routingPreferences.DeadlineTime = schedule.NextEventTime;
        }
        else
        {
            // Default to fast (most NPCs)
            _routingPreferences.Goal = NpcGoal.Fast;
        }

        // Update urgency level
        _routingPreferences.UpdateUrgency(currentTime);
    }

    private async void _advanceAndTravel()
    {
        DateTime gameNow = DateTime.Now; // Will be overridden by daynite controller
        try
        {
            var controller = I.Get<nogame.modules.daynite.Controller>();
            gameNow = controller.GameNow;
        }
        catch (Exception)
        {
            // Fallback if daynite not available
        }

        var storylet = _taleManager.AdvanceNpc(_npcId, gameNow);
        if (storylet == null)
        {
            // No valid storylet, stay idle
            _stayAt.StayDurationSeconds = 10f;
            TriggerStrategy("activity");
            return;
        }

        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null) return;

        // Update routing preferences before computing route
        UpdateRoutingPreferences(gameNow);

        // Get world position for the destination location
        Vector3 destination = _taleManager.GetWorldPosition(_npcId, gameNow);

        // Compute street route asynchronously before moving
        // NPC stays in current state while pathfinding runs (typically < 100ms)
        // If pathfinding returns null, GoToStrategyPart will use straight-line fallback
        SegmentRoute route = null;
        try
        {
            var routeGen = I.Get<engine.tale.IRouteGenerator>();
            if (routeGen != null)
            {
                route = await routeGen.GetRouteAsync(_currentPosition.Position, destination, _currentPosition);
            }
        }
        catch (Exception e)
        {
            Logger.Trace($"_advanceAndTravel: Route generation failed: {e.Message}");
            // null route = straight-line fallback
        }

        // Set up go_to
        _goTo.Destination = destination;
        _goTo.CurrentPosition = _currentPosition;
        _goTo.PrecomputedRoute = route;

        TriggerStrategy("travel");
    }


    private void _setupActivity()
    {
        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null) return;

        // Convert game-time duration to real-time seconds
        float gameMinutes = (float)(schedule.CurrentEnd - schedule.CurrentStart).TotalMinutes;
        float realSeconds = gameMinutes / (24f * 60f) * RealSecondsPerGameDay;

        // Clamp to reasonable range (2s to 5min real time)
        realSeconds = Math.Clamp(realSeconds, 2f, 300f);

        _stayAt.StayDurationSeconds = realSeconds;

        // Detect if this is an indoor activity based on location type
        _stayAt.IsIndoorActivity = false;
        var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
        if (spatialModel != null)
        {
            var loc = spatialModel.GetLocation(schedule.CurrentLocationId);
            if (loc != null && loc.Type != "street_segment")
            {
                _stayAt.IsIndoorActivity = true;
            }
        }
    }


    #region IStrategyPart

    public override void OnExit()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        sm.Unsubscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);
        base.OnExit();
    }


    public override void OnEnter()
    {
        base.OnEnter();
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        sm.Subscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);
    }

    #endregion


    private TaleEntityStrategy(
        int npcId,
        TaleManager taleManager,
        CharacterModelDescription cmd,
        PositionDescription startPosition,
        CharacterState characterState)
    {
        _npcId = npcId;
        _taleManager = taleManager;
        _cmd = cmd;
        _startPosition = startPosition;
        _currentPosition = startPosition;
        _characterState = characterState;
        _routingPreferences = new RoutingPreferences();
        _lastPreferenceUpdate = DateTime.MinValue;

        _goTo = new GoToStrategyPart
        {
            Controller = this,
            CharacterModelDescription = cmd,
            CharacterState = characterState,
            Destination = startPosition.Position,
            CurrentPosition = startPosition
        };

        _stayAt = new StayAtStrategyPart
        {
            Controller = this,
            CharacterModelDescription = cmd,
            CharacterState = characterState,
            StayDurationSeconds = 10f
        };

        // Reuse citizen flee/recover
        var walkNavigator = _createFallbackNavigator(startPosition);

        Strategies = new()
        {
            {
                "activity", _stayAt
            },
            {
                "flee", new FleeStrategy
                {
                    Controller = this,
                    CharacterModelDescription = cmd,
                    CharacterState = characterState,
                    Navigator = walkNavigator
                }
            },
            {
                "recover", new RecoverStrategy
                {
                    Controller = this,
                    CharacterModelDescription = cmd,
                    CharacterState = characterState
                }
            },
            {
                "travel", _goTo
            }
        };
    }


    private static SegmentNavigator _createFallbackNavigator(PositionDescription pod)
    {
        // Minimal 2-point route for flee behavior
        var route = new SegmentRoute { LoopSegments = true };
        var pos = pod.Position;
        var up = Vector3.UnitY;
        var right = Vector3.UnitX;

        route.Segments.Add(new SegmentEnd
        {
            Position = pos,
            Up = up,
            Right = right,
            PositionDescription = pod
        });
        route.Segments.Add(new SegmentEnd
        {
            Position = pos + new Vector3(10f, 0f, 10f),
            Up = up,
            Right = right
        });

        return new SegmentNavigator
        {
            SegmentRoute = route,
            Position = pod
        };
    }


    /// <summary>
    /// Factory method: create a TaleEntityStrategy for an NPC with an existing schedule.
    /// </summary>
    public static bool TryCreate(
        NpcSchedule schedule,
        TaleManager taleManager,
        PositionDescription pod,
        CharacterModelDescription cmd,
        out TaleEntityStrategy strategy)
    {
        if (pod == null || taleManager == null || schedule == null)
        {
            strategy = null;
            return false;
        }

        var characterState = new CharacterState
        {
            BasicSpeed = 4.5f / 3.6f // ~4.5 km/h walking speed
        };

        strategy = new TaleEntityStrategy(schedule.NpcId, taleManager, cmd, pod, characterState);
        return true;
    }
}
