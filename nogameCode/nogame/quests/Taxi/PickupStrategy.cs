using System.Numerics;
using engine;
using engine.behave;

namespace nogame.quests.Taxi;

/// <summary>
/// Pickup phase: creates a ToLocation marker at the guest position and waits
/// for the player to reach it.
/// </summary>
public class PickupStrategy : AEntityStrategyPart
{
    public required Vector3 GuestPosition { get; init; }

    private engine.quest.ToLocation _questTarget;


    private void _onReachTarget()
    {
        Controller.GiveUpStrategy(this);
    }


    public override void OnEnter()
    {
        var v3Target = GuestPosition with
        {
            Y = I.Get<engine.world.MetaGen>().Loader.GetHeightAt(GuestPosition) +
                engine.world.MetaGen.ClusterNavigationHeight
        };

        _questTarget = new engine.quest.ToLocation()
        {
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
