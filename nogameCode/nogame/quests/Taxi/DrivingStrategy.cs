using System;
using System.Numerics;
using builtin.tools;
using engine;
using engine.behave;
using engine.joyce;
using engine.world;
using engine.world.components;
using nogame.characters;
using nogame.characters.citizen;
using static engine.Logger;

namespace nogame.quests.Taxi;

/// <summary>
/// Driving phase: creates a ToLocation marker at the destination, spawns
/// an idle waiting person, and waits for the player to reach the target.
/// </summary>
public class DrivingStrategy : AEntityStrategyPart
{
    public required Vector3 DestinationPosition { get; init; }

    private engine.quest.ToLocation _questTarget;
    private DefaultEcs.Entity _waitingPerson;
    private bool _hasWaitingPerson;


    private void _onReachTarget()
    {
        /*
         * Spawn a permanent walking citizen at the destination before ending.
         */
        var sc = I.Get<SpawnController>();
        sc.ForceSpawn(typeof(WalkBehavior), DestinationPosition);

        Controller.GiveUpStrategy(this);
    }


    private async void _spawnWaitingPerson()
    {
        try
        {
            var loader = I.Get<MetaGen>().Loader;
            var v3Ground = loader.GetWalkingPosAt(DestinationPosition);

            if (!loader.TryGetFragment(Fragment.PosToIndex3(v3Ground), out var worldFragment))
            {
                return;
            }

            var rnd = new RandomSource("taxi.guest");
            var cmd = CharacterModelDescriptionFactory.CreateCitizen(rnd);

            var creator = new EntityCreator()
            {
                CharacterModelDescription = cmd,
                PhysicsName = "taxi.guest",
                Position = v3Ground,
                Fragment = worldFragment,
            };

            await creator.CreateAsync();

            var engine = I.Get<Engine>();
            engine.QueueEntitySetupAction(CharacterCreator.EntityName, e =>
            {
                creator.CreateLogical(e);
                _waitingPerson = e;
                _hasWaitingPerson = true;
            });
        }
        catch (Exception e)
        {
            Warning($"DrivingStrategy: Unable to spawn waiting person: {e}");
        }
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
            RelativePosition = v3Target,
            SensitivePhysicsName = nogame.modules.playerhover.MainPlayModule.PhysicsStem,
            SensitiveRadius = 10f,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            MapIcon = MapIcon.IconCode.TaxiTarget,
            OnReachTarget = _onReachTarget
        };
        _questTarget.ModuleActivate();

        _spawnWaitingPerson();
    }


    public override void OnExit()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;

        if (_hasWaitingPerson && _waitingPerson.IsAlive)
        {
            I.Get<HierarchyApi>().Delete(ref _waitingPerson);
        }

        _hasWaitingPerson = false;
    }
}
