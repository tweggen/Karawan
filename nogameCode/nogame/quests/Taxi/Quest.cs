using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.joyce.components;
using engine.quest;
using engine.world;
using engine.world.components;
using OneOf.Types;
using static engine.Logger;

namespace nogame.quests.Taxi;


/**
 * Quest to drive a passenger from a to b.
 *
 * Instantiating this quest selects a citizen far off if none is given
 * and selects a target, if none is given.
 * Then, if the quest marker is hit with the car, a navigation is started.
 *
 * States:
 * -#1 Waiting to reach the citizen
 *    A citizen is spawned to request a taxi
 *    Transition to #2 once the citizen is reached
 * -#2 Wait for reach the target
 *    A target streetpoint is selected
 *    The citizen is made invisible and deactivated in some way.
 * -#3 The taxi has reached the target
 *    The citizen is somehow integrated into the target's citizen flow.
 *    The quest terminates.
 */
public class Quest : AModule, IQuest, ICreator
{
    public string Name { get; set; } = typeof(Quest).FullName;

    private Description _description = new()
    {
        Title = "Be a good cab driver.",
        ShortDescription = "The passenger wants to get to their target.",
        LongDescription = "Isn't this a chase again?"
    };

    public Description GetDescription() => _description;

    private bool _isActive = false;
    public bool IsActive { get => _isActive; set => _isActive = value; }


    public ClusterDesc GuestCluster { get; set; } = new();
    public string RandomSeed { get; set; } = "taxi";

    private DefaultEcs.Entity _eTarget;
    private engine.quest.TrailVehicle _questTarget;

    private void _onReachTarget()
    {
        // TXWTODO: Implement taxi quest completion.
    }


    public async Task CreateEntities()
    {
        return;
    }

    public Func<Task> SetupEntityFrom(Entity eLoaded, JsonElement je)
    {
        return () =>
        {
            return Task.CompletedTask;
        };
    }

    public void SaveEntityTo(Entity eLoader, out JsonNode jn)
    {
        jn = JsonValue.Create($"no additional info from {Name} yet");
        return;
    }


    private async Task<Vector3> _computeGuestLocationAsync()
    {
        Vector3 v3Pos = default;

        PlacementContext pc = new()
        {
            CurrentCluster = GuestCluster,
        };

        PlacementDescription plad = new()
        {
            ReferenceObject = PlacementDescription.Reference.StreetPoint,
            WhichFragment = PlacementDescription.FragmentSelection.AnyFragment,
            WhichCluster = PlacementDescription.ClusterSelection.CurrentCluster,
            WhichQuarter = PlacementDescription.QuarterSelection.AnyQuarter
        };

        PositionDescription pod;

        bool isPlaced = I.Get<Placer>().TryPlacing(new RandomSource(RandomSeed), pc, plad, out pod);
        if (!isPlaced)
        {
            ErrorThrow<InvalidOperationException>($"Unable to place guest in cluster {GuestCluster.Name}.");
        }

        return pod.Position;

    }


    /**
     * As long the quest is in the state find guest, we display the guest marker.
     */
    private void _createGuestMarker(Vector3 v3Marker)
    {

    }


    /**
     * As soon we have picked up the guest we need to create the target marker.
     */

    private void _startRide()
    {
        _engine.Player.CallWithEntity(e =>
            _engine.QueueMainThreadAction(() =>
            {
                /*
                 * Create a destination marker.
                 */
                /*
                 * ... create quest marker and run it.
                 */
                _questTarget = new engine.quest.TrailVehicle()
                {
                    SensitivePhysicsName = nogame.modules.playerhover.MainPlayModule.PhysicsStem,
                    MapCameraMask = nogame.modules.map.Module.MapCameraMask,
                    ParentEntity = _eTarget,
                    OnReachTarget = _onReachTarget
                };

                _questTarget.ModuleActivate();
             }));
    }


    /// <summary>
    /// Start the actual quest.
    /// </summary>
    private async Task _startQuest()
    {
        // TXWTODO: Implement quest start logic.
        await Task.CompletedTask;
    }
    

    /// <summary>
    /// Terminate the quest.
    /// </summary>
    protected override void OnModuleDeactivate()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;

        _isActive = false;
    }


    /// <summary>
    ///  Start the actual quest.
    /// </summary>
    protected override void OnModuleActivate()
    {
        _isActive = true;

        _engine.Run(_startQuest);
    }
}
