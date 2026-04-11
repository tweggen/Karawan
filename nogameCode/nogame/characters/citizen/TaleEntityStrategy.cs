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
using Behavior = engine.behave.components.Behavior;

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

    private float _positionSyncTimer = 0f;
    private const float PositionSyncInterval = 2f; // seconds

    /// <summary>
    /// Tier 2 mode: entity alive but strategy frozen (noticed but dematerialized).
    /// </summary>
    internal bool _isTier2 = false;

    /// <summary>
    /// Seconds of real time per game day. Used to convert game-time
    /// storylet durations to real-time StayAt durations.
    /// </summary>
    public float RealSecondsPerGameDay { get; set; } = 60f * 60f;

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
            Trace($"TALE ENTITY: NPC {_npcId} travel complete, starting activity");
            _setupActivity();
            TriggerStrategy("activity");
        }
        else if (strategy == Strategies["activity"])
        {
            // Activity complete → advance to next storylet (unless in Tier 2)
            Trace($"TALE ENTITY: NPC {_npcId} activity complete, advancing schedule");
            if (!_isTier2)
                _advanceAndTravel();
            else
                Trace($"TALE ENTITY: NPC {_npcId} in Tier 2, not advancing");
        }
        else if (strategy == Strategies["flee"] || strategy == Strategies["recover"])
        {
            // After flee/recover → resume activity with fresh schedule state
            Trace($"TALE ENTITY: NPC {_npcId} flee/recover complete, resuming activity");
            _setupActivity();
            TriggerStrategy("activity");
        }
    }


    /// <summary>
    /// Enter Tier 2 mode: freeze strategy indefinitely (notice by player but far from render distance).
    /// Map icon remains visible on its own camera layer.
    /// </summary>
    public void EnterTier2Mode()
    {
        _isTier2 = true;
        _stayAt.StayDurationSeconds = float.MaxValue; // freeze indefinitely
        TriggerStrategy("activity");
        Trace($"TALE ENTITY: NPC {_npcId} entering Tier 2 mode (noticed, dematerialized).");
    }

    /// <summary>
    /// Exit Tier 2 mode: resume normal schedule progression (back in render distance).
    /// </summary>
    public void ExitTier2Mode()
    {
        _isTier2 = false;
        Trace($"TALE ENTITY: NPC {_npcId} exiting Tier 2 mode, resuming schedule.");
        _advanceAndTravel();
    }


    public override string GetStartStrategy()
    {
        return "activity";
    }


    /// <summary>
    /// Update routing preferences urgency based on current schedule.
    /// Role-based goals are set during NPC creation and persist.
    /// Only urgency is updated dynamically based on deadline proximity.
    /// </summary>
    private void UpdateRoutingPreferences(DateTime currentTime)
    {
        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null)
            return;

        // Copy schedule's RoutingPreferences (includes role-based goal)
        _routingPreferences = schedule.RoutingPreferences;

        // Update deadline and urgency for on-time/safe routing
        if (schedule.NextEventTime.HasValue)
        {
            _routingPreferences.DeadlineTime = schedule.NextEventTime;
        }

        _routingPreferences.UpdateUrgency(currentTime);
    }

    /// <summary>
    /// Sync current entity world position back to the NPC schedule.
    /// This ensures position is preserved during dematerialization.
    /// </summary>
    private void _syncPositionToSchedule()
    {
        if (_entity.IsAlive && _entity.Has<engine.joyce.components.Transform3ToWorld>())
        {
            var worldPos = _entity.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
            var schedule = _taleManager.GetSchedule(_npcId);
            if (schedule != null)
            {
                schedule.CurrentWorldPosition = worldPos;
            }
        }
    }

    private async void _advanceAndTravel()
    {
        // Diagnostic: show current location type before advancing
        var tempSchedule = _taleManager.GetSchedule(_npcId);
        var spatialModel = _taleManager.GetSpatialModel(tempSchedule?.ClusterIndex ?? -1);
        var currentLoc = spatialModel?.GetLocation(tempSchedule?.CurrentLocationId ?? -1);
        string currentLocType = currentLoc?.Type ?? "UNKNOWN";
        Trace($"TALE ENTITY: NPC {_npcId} _advanceAndTravel called FROM location type '{currentLocType}'");

        // Remove conversation behavior when leaving activity phase
        if (_entity.IsAlive && _entity.Has<Behavior>())
        {
            var behavior = _entity.Get<Behavior>();
            if (behavior.Provider is TaleConversationBehavior)
            {
                _entity.Remove<Behavior>();
                Trace($"TALE ENTITY: NPC {_npcId} conversation behavior detached");
            }
        }

        // Update current position to where the NPC actually is now
        if (_entity.IsAlive && _entity.Has<engine.joyce.components.Transform3ToWorld>())
        {
            var worldPos = _entity.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
            _currentPosition.Position = worldPos;
        }

        // Sync position before departing to destination
        _syncPositionToSchedule();

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

        // Debug: What location did we advance to?
        var currentStorylet = _taleManager.GetCurrentStorylet(_npcId);
        string locationName = currentStorylet?.LocationRef ?? "UNKNOWN";

        // Update routing preferences before computing route
        UpdateRoutingPreferences(gameNow);

        // Get destination location's actual position (not NPC's current position)
        spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
        var destLoc = spatialModel?.GetLocation(schedule.CurrentLocationId);
        string destLocType = destLoc?.Type ?? "UNKNOWN";

        // Use destination location's entry point if available, else fall back to position
        Vector3 destination = destLoc != null
            ? (destLoc.EntryPosition != Vector3.Zero ? destLoc.EntryPosition : destLoc.Position)
            : Vector3.Zero;

        // Calculate distance from NPC's current position to destination location
        float distToDestination = Vector3.Distance(_currentPosition.Position, destination);

        // Log for debugging
        Vector3 destLocPos = destLoc?.Position ?? Vector3.Zero;
        Vector3 destLocEntry = destLoc?.EntryPosition ?? Vector3.Zero;

        Trace($"TALE ENTITY: NPC {_npcId} -> LocationId={schedule.CurrentLocationId} '{locationName}' (type={destLocType}, distance={distToDestination:F2}m)");

        // Special case: "current" location means stay in place and do activity, don't travel
        if (locationName == "current")
        {
            Trace($"TALE ENTITY: NPC {_npcId} location is 'current' (stay in place), skipping travel and triggering activity");
            _setupActivity();
            TriggerStrategy("activity");
            return;
        }

        // Special case: Already at destination (within 2m) - do activity instead of travel
        if (distToDestination < 2.0f)
        {
            Trace($"TALE ENTITY: NPC {_npcId} already at destination (distance={distToDestination:F2}m < 2m threshold), skipping travel and triggering activity");
            _setupActivity();
            TriggerStrategy("activity");
            return;
        }

        // Compute street route asynchronously before moving
        // NPC stays in current state while pathfinding runs (typically < 100ms)
        // If pathfinding returns null, GoToStrategyPart will use straight-line fallback
        SegmentRoute route = null;
        try
        {
            var routeGen = I.Get<engine.tale.IRouteGenerator>();
            if (routeGen != null)
            {
                // Pass routing preferences for multi-objective pathfinding
                route = await routeGen.GetRouteAsync(_currentPosition.Position, destination, _currentPosition,
                    _routingPreferences, schedule.PreferredTransportationType);
                if (route != null)
                    Trace($"TALE ENTITY: NPC {_npcId} got NavMesh route ({route.Segments.Count} segments)");
                else
                    Trace($"TALE ENTITY: NPC {_npcId} route generation returned null, using straight-line fallback");
            }
            else
            {
                Trace($"TALE ENTITY: NPC {_npcId} IRouteGenerator not registered, using straight-line fallback");
            }
        }
        catch (Exception e)
        {
            Trace($"TALE ENTITY: NPC {_npcId} route generation exception: {e.Message}");
            Logger.Trace($"_advanceAndTravel: Route generation failed: {e.Message}");
            // null route = straight-line fallback
        }

        // Fallback: if route is null and destination is far away but unreachable,
        // location is likely isolated (dead-end street or disconnected venue).
        // Stay at current location instead of using straight-line fallback through buildings.
        if (route == null && distToDestination > 10f)
        {
            Trace($"TALE ENTITY: NPC {_npcId} destination '{locationName}' unreachable (distance={distToDestination:F0}m but no path). Staying at current location instead.");
            _setupActivity();
            TriggerStrategy("activity");
            return;
        }

        // Set up go_to
        _goTo.Destination = destination;
        _goTo.CurrentPosition = _currentPosition;
        _goTo.PrecomputedRoute = route;

        if (route == null)
            Trace($"TALE ENTITY: NPC {_npcId} triggering travel with NULL route (will use straight-line fallback)");
        else
            Trace($"TALE ENTITY: NPC {_npcId} triggering travel with route ({route.Segments.Count} segments)");

        // Enable "E to Talk" during travel for TALE NPCs
        _goTo.TravelBehaviorFactory = (navigator) => new TaleWalkBehavior(_npcId, this)
        {
            CharacterModelDescription = _cmd,
            Navigator = navigator
        };

        TriggerStrategy("travel");
    }


    private void _setupActivity()
    {
        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null) return;

        // Clear transit phase on arrival at destination
        schedule.IsInTransit = false;

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
            if (loc != null)
            {
                if (loc.Type != "street_segment")
                {
                    _stayAt.IsIndoorActivity = true;
                    Trace($"TALE ENTITY: NPC {_npcId} marked as indoor at location type '{loc.Type}'");
                }
                else
                {
                    Trace($"TALE ENTITY: NPC {_npcId} marked as OUTDOOR at location type '{loc.Type}'");
                }
            }
            else
            {
                Trace($"TALE ENTITY: NPC {_npcId} location {schedule.CurrentLocationId} NOT FOUND in spatial model!");
            }
        }
        else
        {
            Trace($"TALE ENTITY: NPC {_npcId} spatial model NULL for cluster {schedule.ClusterIndex}!");
        }

        // Set conversation behavior for outdoor NPCs (used by StayAtStrategyPart.OnEnter)
        if (!_stayAt.IsIndoorActivity)
        {
            try
            {
                var conversationBehavior = new TaleConversationBehavior(_npcId, this)
                {
                    CharacterModelDescription = _cmd
                };
                _stayAt.ActivityBehavior = conversationBehavior;
                Trace($"TALE ENTITY: NPC {_npcId} conversation behavior prepared");
            }
            catch (Exception e)
            {
                _stayAt.ActivityBehavior = null;
                Trace($"TALE ENTITY: NPC {_npcId} failed to prepare conversation behavior: {e.Message}");
            }
        }
        else
        {
            _stayAt.ActivityBehavior = null;
        }

        // Sync position now that we've arrived at destination
        _syncPositionToSchedule();
    }


    /// <summary>
    /// Called when an NPC materializes mid-transit. Starts in travel state
    /// heading to TransitToLocationId instead of entering activity first.
    /// </summary>
    public async void SpawnInTravel()
    {
        var schedule = _taleManager.GetSchedule(_npcId);
        if (schedule == null || !schedule.IsInTransit)
        {
            Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel called but not in transit (schedule exists: {schedule != null})");
            return;
        }

        var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
        var destLoc = spatialModel?.GetLocation(schedule.TransitToLocationId);
        if (destLoc == null)
        {
            Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel failed: destination location {schedule.TransitToLocationId} not found");
            return;
        }

        Vector3 dest = destLoc.EntryPosition != Vector3.Zero ? destLoc.EntryPosition : destLoc.Position;
        float distToDestination = Vector3.Distance(_currentPosition.Position, dest);

        // Compute route using the same pathfinding as _advanceAndTravel
        SegmentRoute route = null;
        try
        {
            var routeGen = I.Get<engine.tale.IRouteGenerator>();
            if (routeGen != null)
            {
                route = await routeGen.GetRouteAsync(_currentPosition.Position, dest, _currentPosition,
                    _routingPreferences, schedule.PreferredTransportationType);
                if (route != null)
                    Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel got route ({route.Segments.Count} segments)");
                else
                    Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel route returned null");
            }
        }
        catch (Exception e)
        {
            Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel route exception: {e.Message}");
        }

        // If no route and far away, stay in place rather than walking through buildings
        if (route == null && distToDestination > 10f)
        {
            Trace($"TALE ENTITY: NPC {_npcId} SpawnInTravel destination unreachable (distance={distToDestination:F0}m, no path). Staying.");
            _setupActivity();
            TriggerStrategy("activity");
            return;
        }

        _goTo.Destination = dest;
        _goTo.CurrentPosition = _currentPosition;
        _goTo.PrecomputedRoute = route;
        // Enable "E to Talk" during travel for TALE NPCs
        _goTo.TravelBehaviorFactory = (navigator) => new TaleWalkBehavior(_npcId, this)
        {
            CharacterModelDescription = _cmd,
            Navigator = navigator
        };

        Trace($"TALE ENTITY: NPC {_npcId} spawning in travel to {dest} (from {_currentPosition.Position}, route={route?.Segments.Count ?? 0} segments)");
        TriggerStrategy("travel");
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
        // Configure initial activity BEFORE base.OnEnter triggers the start strategy,
        // so StayAtStrategyPart.OnEnter has ActivityBehavior (TaleConversationBehavior) set.
        Trace($"TALE ENTITY: NPC {_npcId} OnEnter: calling _setupActivity before base.OnEnter");
        _setupActivity();
        Trace($"TALE ENTITY: NPC {_npcId} OnEnter: ActivityBehavior={_stayAt.ActivityBehavior?.GetType().Name ?? "null"}");
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

        // Initialize routing preferences from the NPC's schedule (set during generation)
        var schedule = taleManager.GetSchedule(npcId);
        _routingPreferences = schedule?.RoutingPreferences ?? new RoutingPreferences();

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
