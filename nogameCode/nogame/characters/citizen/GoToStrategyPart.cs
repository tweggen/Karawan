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
    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }

    /// <summary>Set by TaleEntityStrategy before TriggerStrategy("travel").</summary>
    public Vector3 Destination { get; set; }

    /// <summary>Set by TaleEntityStrategy before TriggerStrategy("travel").</summary>
    public PositionDescription CurrentPosition { get; set; }

    /// <summary>Optional pre-computed route (from StreetRouteBuilder). If null, uses straight-line.</summary>
    public SegmentRoute PrecomputedRoute { get; set; }

    private SegmentNavigator _navigator;
    private WalkBehavior _walkBehavior;
    private float _totalDistance;
    private bool _arrived;


    public override void OnEnter()
    {
        _arrived = false;

        var startPos = CurrentPosition?.Position ?? Vector3.Zero;
        var endPos = Destination;

        // Compute proper ground height based on cluster/quarter geometry
        float groundHeight = 0f;
        if (CurrentPosition?.ClusterDesc != null)
        {
            groundHeight = CurrentPosition.ClusterDesc.AverageHeight +
                          engine.world.MetaGen.ClusterStreetHeight +
                          engine.world.MetaGen.QuarterSidewalkOffset;
        }

        // Apply proper Y coordinate to both route points
        startPos.Y = groundHeight;
        endPos.Y = groundHeight;

        _totalDistance = Vector3.Distance(startPos, endPos);

        if (_totalDistance < 0.5f)
        {
            // Already at destination, signal immediately
            _arrived = true;
            _engine.QueueEventHandler(() => Controller.GiveUpStrategy(this));
            return;
        }

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
        }
        else
        {
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

        _walkBehavior = new WalkBehavior
        {
            CharacterModelDescription = CharacterModelDescription,
            Navigator = _navigator
        };

        _entity.Set(new engine.behave.components.Behavior(_walkBehavior));
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
