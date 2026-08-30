using DefaultEcs;
using engine;
using engine.behave;

namespace nogame.quests.HelloFishmonger;

public class TrailStrategy : AEntityStrategyPart
{
    public required Entity CarEntity { get; init; }

    private engine.quest.TrailVehicle _questTarget;


    private void _onReachTarget()
    {
        Controller.GiveUpStrategy(this);
    }


    public override void OnEnter()
    {
        _questTarget = new engine.quest.TrailVehicle()
        {
            OwnerQuestEntity = _entity,
            /*
             * The player is sensed by the hover ship's own physics name below, so
             * this guideline is for a driver: it has to be routed and drawn over car
             * lanes rather than over the pavement.
             */
            TransportType = engine.navigation.TransportationType.Car,
            SensitivePhysicsName = nogame.modules.playerhover.MainPlayModule.PhysicsStem,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            ParentEntity = CarEntity,
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
