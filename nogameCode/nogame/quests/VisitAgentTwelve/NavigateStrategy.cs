using System.Numerics;
using engine;
using engine.behave;

namespace nogame.quests.VisitAgentTwelve;

/// <summary>
/// Navigate-to-location phase: creates a ToLocation marker and waits
/// for the player to reach it.
/// </summary>
public class NavigateStrategy : AEntityStrategyPart
{
    public required Vector3 DestinationPosition { get; init; }

    private engine.quest.ToLocation _questTarget;


    private void _onReachTarget()
    {
        Controller.GiveUpStrategy(this);
    }


    public override void OnEnter()
    {
        var v3Target = DestinationPosition with
        {
            Y = I.Get<engine.world.MetaGen>().Loader.GetHeightAt(DestinationPosition) +
                engine.world.MetaGen.ClusterNavigationHeight
        };

        _questTarget = new engine.quest.ToLocation()
        {
            OwnerQuestEntity = _entity,
            /*
             * The player is sensed by the hover ship's own physics name below, so
             * this guideline is for a driver: it has to be routed and drawn over car
             * lanes rather than over the pavement.
             */
            TransportType = engine.navigation.TransportationType.Car,
            RelativePosition = v3Target,
            SensitivePhysicsName = nogame.modules.playerhover.MainPlayModule.PhysicsStem,
            SensitiveRadius = 10f,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            OnReachTarget = _onReachTarget
        };
        _questTarget.ModuleActivate();
    }


    public override void OnExit()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;
    }
}
