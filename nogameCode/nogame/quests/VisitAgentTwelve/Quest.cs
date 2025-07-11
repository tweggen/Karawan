using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.joyce.components;
using engine.quest;
using engine.world;
using engine.world.components;
using static engine.Logger;

namespace nogame.quests.VisitAgentTwelve;

public class Quest : AModule, IQuest, ICreator
{
    private Description _description = new()
    {
        Title = "Come to the location.",
        ShortDescription = "Try to find the marker on the map and reach it.",
        LongDescription = "Every journey starts with the first step. Reach for the third step" +
                          " to make it an experience."
    };


    private bool _isActive = false;

    public Vector3 DestinationPosition { get; set; }
    
    private engine.quest.ToLocation _questTarget;
    
    private void _onReachTarget()
    {
        // TXWTODO: Is this the proper way to advance the logic?
        I.Get<engine.quest.Manager>().DeactivateQuest(this);
        I.Get<nogame.modules.story.Narration>().TriggerPath("agent12");
    }

    
    public string Name { get; set; } = typeof(Quest).FullName;

    public Description GetDescription()
    {
        return _description;
    }

    
    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }

    /**
     * Create the ToLocation submodule, including the quest marker.
     * This is based on the destination point given to us.
     */
    private void _createToLocation(Vector3 v3Marker)
    {
        var v3Target = v3Marker with
        {
            Y = I.Get<engine.world.MetaGen>().Loader.GetHeightAt(v3Marker) +
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


    private async Task<Vector3> _computeTargetLocationLT()
    {
        Vector3 v3Pos = default;
        
        await _engine.TaskMainThread(() =>
        {

            /*
             * Test code: Find any bar.
             * This is not smart. We just look for the one closest to the player.
             */
            var withIcon =
                _engine.GetEcsWorld().GetEntities().With<Transform3ToWorld>().With<MapIcon>().AsEnumerable();
            float mind2 = Single.MaxValue;


            DefaultEcs.Entity eClosest = default;

            if (_engine.Player.TryGet(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
            {
                var v3Player = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
                foreach (var e in withIcon)
                {
                    ref var cMapIcon = ref e.Get<MapIcon>();
                    if (cMapIcon.Code != MapIcon.IconCode.Drink) continue;
                    var v3D = (v3Player - e.Get<Transform3ToWorld>().Matrix.Translation);
                    var d2d2 = (new Vector2(v3D.X, v3D.Z)).LengthSquared();
                    if (d2d2 > 20f && d2d2 < mind2)
                    {
                        if (e.IsAlive && e.IsEnabled())
                        {
                            eClosest = e;
                            mind2 = d2d2;
                        }
                    }
                }
            }

            if (default == eClosest)
            {
                Error($"Unable to find any mark close to player entity.");
                v3Pos = Vector3.Zero;
            }

            v3Pos = eClosest.Get<Transform3ToWorld>().Matrix.Translation;
        });
        
        return v3Pos;
    }
    

    protected override void OnModuleDeactivate()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;
        
        _isActive = false;
    }


    protected override void OnModuleActivate()
    {
        _isActive = true;
        
        _engine.Player.CallWithEntity(e => 
            _engine.QueueMainThreadAction(() =>
        {
            /*
             * Create a destination marker.
             */
            _createToLocation(DestinationPosition);
        }));
    }


    /**
     * Re-create this quest's entities while deserializing.
     *
     * This quest requires a quest target marker that is supposed to be
     * recreated. Until this fully is automated, I recreate it manually.
     */
    public Func<Task> SetupEntityFrom(Entity eLoaded, JsonElement je) => new(async () =>
    {
        /*
         * No additional data to use.
         */
    });

    
    /**
     * Serialize non-basic information about this quest's entities.
     * This is used to save e.g. our mission car.
     */
    public void SaveEntityTo(Entity eLoader, out JsonNode jn)
    {
        jn = JsonValue.Create("no additional info from VisitAgentTwelve yet.");
    }


    public async Task CreateEntities()
    {
        DestinationPosition = await _computeTargetLocationLT();
    }
    

    public Quest()
    {
        _engine = I.Get<Engine>();
    }

    
    public static IQuest Instantiate()
    {
        var quest = new Quest();
        
        return quest;
    }
}

