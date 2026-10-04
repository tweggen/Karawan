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

                /*
                 * Without this the passenger renders in its bind pose - a T-pose - for
                 * as long as the quest runs, standing at a street point on the sidewalk.
                 *
                 * Nothing here ever called SetAnimation at all, so
                 * AnimationState.ModelAnimation stayed null, CameraOutput substituted
                 * NullAnimationsEntry, and the vertex shader skipped skinning entirely.
                 */
                InitialAnimName = cmd.IdleAnimName,

                /*
                 * ...and this is what RETRIES it. InitialAnimName is one call, issued
                 * before ModelCache has necessarily attached FromModel, so on its own it
                 * is a coin toss rather than a driver.
                 *
                 * IdleBehavior is not an option here: its OnAttach takes a ref to the Body
                 * component, and this entity has no physics object (no
                 * CollisionPropertiesFactory), so DefaultEcs would hand it a reference into
                 * unused storage. AnimationOnlyBehavior touches nothing else.
                 */
                BehaviorFactory = e => new nogame.characters.citizen.AnimationOnlyBehavior()
                {
                    AnimName = cmd.IdleAnimName
                },
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
        /*
         * The height of the SURFACE the city is built on, not of the terrain under it plus
         * the vehicle hover clearance.
         *
         * The marker cube rests on this position (QuestMarker), so this is where its
         * bottom face lands. GetHeightAt is the terrain, and in a city that keeps its
         * terrain the terrain is not the road: measured at every junction of the four
         * baseline cities, terrain + ClusterNavigationHeight put the cube's bottom below
         * the pavement at 88 to 93 % of them, 0.65 m at the median and 9.8 m at the worst.
         * A quest destination is placed at a junction (engine.Placer, Reference.StreetPoint),
         * and a junction is the one place where the built surface has an exact height.
         */
        var v3Target = DestinationPosition with
        {
            Y = I.Get<engine.world.MetaGen>().Loader.GetCitySurfaceHeightAt(DestinationPosition)
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
