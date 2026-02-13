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
