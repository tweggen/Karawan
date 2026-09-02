using System;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Strategy part: walk NPC to a destination position using a SegmentRoute.
/// TaleEntityStrategy sets Destination/CurrentPosition before triggering this.
/// If PrecomputedRoute is set, uses that; otherwise falls back to 2-point straight-line.
/// Signals GiveUpStrategy when the NPC reaches the destination.
/// </summary>
public class GoToStrategyPart : AEntityStrategyPart
{
    private static readonly engine.Dc _dc = engine.Dc.CitizenStrategy;

    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }

    /// <summary>Set by TaleEntityStrategy before TriggerStrategy("travel").</summary>
    public Vector3 Destination { get; set; }

    /// <summary>Set by TaleEntityStrategy before TriggerStrategy("travel").</summary>
    public PositionDescription CurrentPosition { get; set; }

    /// <summary>Optional pre-computed route (from StreetRouteBuilder). If null, uses straight-line.</summary>
    public SegmentRoute PrecomputedRoute { get; set; }

    /// <summary>
    /// Optional factory to create a custom behavior wrapping the walk navigator.
    /// If set, receives the SegmentNavigator and returns the behavior to use
    /// instead of the default WalkBehavior. Used by TaleEntityStrategy to create
    /// TaleWalkBehavior with conversation support.
    /// </summary>
    public Func<SegmentNavigator, engine.behave.IBehavior> TravelBehaviorFactory { get; set; } = null;

    private SegmentNavigator _navigator;
    private WalkBehavior _walkBehavior;
    private float _totalDistance;
    private bool _arrived;


    /**
     * Where a walker's feet go at a position on this pod's own block, or on the terrain
     * where there is no block to ask.
     *
     * The block, when the pod names one and the position is on it: a block's pavement is
     * the block floor's top face, and BuildingFooting.GroundAt answers from the boundary
     * edge nearest the point, interpolated between its two corners' own junction heights.
     * Measured over the block edges of the four baseline cities on the shipped terrain, the
     * conformed terrain runs 5.5 m below that floor and 6.3 m above it, and is below it on
     * 43 to 51 % of edges - because the conforming pass grades the ground toward the
     * streets on a 20 m grid with a 60 m smoothstep, so the middle of a block is only about
     * half way there.
     *
     * The AABB test is the honest limit of what this can claim: the destination of a travel
     * may well be on a different block from the one the walker started on, and answering
     * from the wrong block would be worse than answering from the terrain.
     */
    private float _walkingHeightAt(in Vector3 v3World)
    {
        var cd = CurrentPosition?.ClusterDesc;
        var q = CurrentPosition?.Quarter;

        if (null != cd)
        {
            Vector3 v3Cluster = v3World - cd.Pos;

            if (engine.streets.generation.BuildingFooting.TryPavementHeightAt(
                    q, new Vector2(v3Cluster.X, v3Cluster.Z), out float onBlock))
            {
                return onBlock;
            }
        }

        return builtin.modules.satnav.desc.NavJunction.WalkingHeightOf(
            cd?.GroundHeightAt(v3World) ?? 0f);
    }


    public override void OnEnter()
    {
        _arrived = false;

         var startPos = CurrentPosition?.Position ?? Vector3.Zero;
        var endPos = Destination;

        // Compute street-level Y from cluster geometry.
        // PositionDescription.ClusterDesc must be set by the spawner; if it is null
        // (e.g. a TALE pod without ClusterDesc), fall back to the incoming position Y.
        // Never allow Y=0 as it puts NPCs below all terrain.
        float groundHeight = 0f;
        float endGroundHeight = 0f;
        if (CurrentPosition?.ClusterDesc != null)
        {
            /*
             * Sampled at each end rather than once at the start. This is only the
             * straight-line fallback - there are no lanes here to take a height from - but
             * "one Y for the whole route" is the same defect that made a routed walk across
             * a hill come out flat, and it costs one more sample to not have it. Identical
             * in a flat city, where both samples are the average.
             */
            groundHeight = _walkingHeightAt(startPos);
            endGroundHeight = _walkingHeightAt(endPos);
        }
        else if (startPos.Y != 0f)
        {
            // Position already has a valid Y — preserve it
            groundHeight = startPos.Y;
            endGroundHeight = startPos.Y;
        }

        // Apply proper Y coordinate to both route points
        startPos.Y = groundHeight;
        endPos.Y = endGroundHeight;

        _totalDistance = Vector3.Distance(startPos, endPos);

        if (_totalDistance < 0.5f)
        {
            // Already at destination, signal immediately
            _arrived = true;
            Trace(_dc, $"GoToStrategyPart: Distance {_totalDistance:F2}m < 0.5m, already at destination, signaling completion");
            _engine.QueueEventHandler(() => Controller.GiveUpStrategy(this));
            return;
        }

        string routeType = PrecomputedRoute != null ? "precomputed route" : "straight-line";
        Trace(_dc, $"GoToStrategyPart: Distance {_totalDistance:F2}m, will use {routeType}");

        var forward = Vector3.Normalize(endPos - startPos);
        if (float.IsNaN(forward.X)) forward = Vector3.UnitX;
        var up = Vector3.UnitY;
        var right = Vector3.Cross(forward, up);
        if (right.LengthSquared() < 0.001f) right = Vector3.UnitX;

        // Use precomputed route if available, else fall back to straight-line
        SegmentRoute route;
        if (PrecomputedRoute != null)
        {
            route = PrecomputedRoute;
            Trace(_dc, $"GoToStrategyPart: Using precomputed route with {route.Segments.Count} segments from {startPos} to {endPos}");
        }
        else
        {
            Trace(_dc, $"GoToStrategyPart: No precomputed route, using straight-line from {startPos} to {endPos}");
            route = new SegmentRoute();
            route.Segments.Add(new SegmentEnd
            {
                Position = startPos,
                Up = up,
                Right = right,
                PositionDescription = CurrentPosition
            });
            route.Segments.Add(new SegmentEnd
            {
                Position = endPos,
                Up = up,
                Right = right
            });
        }

        _navigator = new SegmentNavigator
        {
            SegmentRoute = route,
            Position = CurrentPosition
        };
        _navigator.Speed = CharacterState.BasicSpeed;

        if (TravelBehaviorFactory != null)
        {
            var customBehavior = TravelBehaviorFactory(_navigator);
            _entity.Set(new engine.behave.components.Behavior(customBehavior));
        }
        else
        {
            _walkBehavior = new WalkBehavior
            {
                CharacterModelDescription = CharacterModelDescription,
                Navigator = _navigator
            };
            _entity.Set(new engine.behave.components.Behavior(_walkBehavior));
        }
    }


    public override void OnExit()
    {
        if (!_arrived || _walkBehavior != null)
        {
            if (_entity.IsAlive && _entity.Has<engine.behave.components.Behavior>())
                _entity.Remove<engine.behave.components.Behavior>();
        }

        _navigator = null;
        _walkBehavior = null;
    }


    public override void Sync(in Entity entity)
    {
        if (_arrived || _navigator == null) return;

        _navigator.NavigatorGetTransformation(out var pos, out _);
        float distToEnd = Vector3.Distance(pos, Destination);

        if (distToEnd < 1.0f)
        {
            _arrived = true;
            _engine.QueueEventHandler(() => Controller.GiveUpStrategy(this));
        }
    }


    #region IEntityStrategy

    public override void OnDetach(in Entity entity)
    {
        _walkBehavior = null;
        _navigator = null;
        base.OnDetach(entity);
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
    }

    #endregion
}
