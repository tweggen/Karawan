using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.quest;
using engine.streets;
using engine.world;
using engine.world.components;
using nogame.characters.car3;
using nogame.cities;
using static engine.Logger;
using Behavior = engine.behave.components.Behavior;

namespace nogame.quests.HelloFishmonger;

public class Quest : AModule, IQuest, ICreator
{
    private ModelCacheParams _mcp;
    private Model _model;
    private bool _isActive = false;

    private DefaultEcs.Entity _eTarget;
    private engine.quest.TrailVehicle _questTarget;  
    
    private Description _description = new()
    {
        Title = "Trail the car.",
        ShortDescription = "The car quickly is departing. Follow it to its target!",
        LongDescription = "Isn't this a chase again?"
    };


    private void _onReachTarget()
    {
        I.Get<engine.quest.Manager>().DeactivateQuest(this);
        I.Get<nogame.modules.story.Narration>().TriggerPath("firstPubSecEncounter");
    }


    public string Name { get; set; } = typeof(Quest).FullName;


    private string _targetCarName = "Fishmonger's car";
    
    public Description GetDescription()
    {
        return _description;
    }

    
    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }
    

    private void _selectStartPoint(
        out ClusterDesc clusterDesc,
        out Fragment worldFragment,
        out StreetPoint streetPoint)
    {
        float minDist = 100f;
        float minDist2 = minDist * minDist;

        clusterDesc = I.Get<engine.world.ClusterList>().GetClusterAt(Vector3.Zero);
        Vector3 v3Cluster = clusterDesc.Pos;
        var listStreetPoints = clusterDesc.StrokeStore().GetStreetPoints();
        _engine.Player.TryGet(out var ePlayer);

        StreetPoint? spStart = listStreetPoints.FirstOrDefault(
            sp =>
                (sp.Pos3 with { Y = 0f } - ePlayer.Get<Transform3ToWorld>().Matrix.Translation with { Y = 0 })
                .LengthSquared() >= minDist2
                && I.Get<MetaGen>().Loader.TryGetFragment(
                    Fragment.PosToIndex3(sp.Pos3 + v3Cluster), out _));
        if (null == spStart)
        {
            spStart = listStreetPoints.First();
        }

        I.Get<MetaGen>().Loader.TryGetFragment(Fragment.PosToIndex3(spStart.Pos3 + v3Cluster), out worldFragment);
        streetPoint = spStart;
    }


    private void _startQuest()
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
                    SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
                    MapCameraMask = nogame.modules.map.Module.MapCameraMask,
                    ParentEntity = _eTarget,
                    OnReachTarget = _onReachTarget
                };
                // TXWTODO: Run only with player available
                _questTarget.ModuleActivate();
            }));
    }
    

    public override void ModuleDeactivate()
    {
        _questTarget?.ModuleDeactivate();
        _questTarget?.Dispose();
        _questTarget = null;
        
        _engine.RemoveModule(this);
        _isActive = false;
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _isActive = true;
        _engine.AddModule(this);

        _engine.Run(_startQuest);
    }

    
    /**
     * Re-create this quest's entities while deserializing.
     * We noted ourselves as creator to the car we need to chase.
     * Therefore serialization will have taken care to serialize
     * the basic car entity.
     */
    public Func<Task> SetupEntityFrom(Entity eLoaded, JsonElement je) => new(async () =>
    {
        /*
         * WE have the basic car entity and the quest object. The quest object,
         * however, does neither have the TrailVehicle objecz set up nor the
         * cat entity associated.
         */

        var quest = eLoaded.Get<engine.quest.components.Quest>().ActiveQuest;
        if (quest.IsActive == false)
        {
            /*
             * No need to setup anything if the quest is not active.
             */
            return;
        }
        
        
        /*
         * Find our car to set it up.
         */
        var eTarget = _engine.GetEcsWorld().GetEntities()
            .With<Creator>().With<Behavior>().With<EntityName>()
            .AsEnumerable().FirstOrDefault(e =>
                e.Get<EntityName>().Name == _targetCarName);

        if (eTarget == default)
        {
            ErrorThrow<InvalidOperationException>("Unable to find target car in data set.");
            return;
        }
        return;
    });

    
    /**
     * Serialize non-basic information about this quest's entities.
     * This is used to save e.g. our mission car. 
     */
    public void SaveEntityTo(Entity eLoader, out JsonNode jn)
    {
        jn = JsonValue.Create("no additional info from HelloFishmonger yet"); 
    }


    /**
     * Create the car entity that we are supposed to chase.
     */
    public async Task CreateEntities()
    {
        /*
         * Load and configure model in arbitrary thread.
         */
        _mcp = new()
        {
            Url = "car6.obj",
            Properties = new()
            {
                ["primarycolor"] = "#ffffff00"
            },
            Params = new()
            {
                GeomFlags = 0 | InstantiateModelParams.CENTER_X
                              | InstantiateModelParams.CENTER_Z
                              | InstantiateModelParams.ROTATE_Y180
                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                              | InstantiateModelParams.BUILD_PHYSICS
                              | InstantiateModelParams.PHYSICS_DETECTABLE
                              | InstantiateModelParams.PHYSICS_TANGIBLE
                              | InstantiateModelParams.PHYSICS_CALLBACKS
                ,
                MaxDistance = 150,
                CollisionLayers = 0x0002,
                Name = _targetCarName
            }
        };
        
        _model = await I.Get<ModelCache>().LoadModel(_mcp);
        
        _selectStartPoint(out var clusterDesc, out var worldFragment, out var streetPoint);

        await _engine.TaskMainThread(() =>
        {
            _eTarget = _engine.CreateEntity(_targetCarName);
            
            /*
             * Create the basic car.
             */
            CharacterCreator.SetupCharacterMT(
                _eTarget,
                clusterDesc, worldFragment, streetPoint,
                _model, _mcp,
                new characters.car3.Behavior()
                {
                    Navigator = new StreetNavigationController()
                    {
                        ClusterDesc = clusterDesc,
                        StartPoint = streetPoint,
                        Seed = 100,
                        Speed = 35f
                    }
                },
                null
            );

            /*
             * Make it our car.
             */
            _eTarget.Set(new Creator()
            {
                Id = I.Get<CreatorRegistry>().FindCreatorId(I.Get<engine.quest.Manager>())
            });

            /*
             * Make it mission critical, so that it is not purged due to unloaded fragments.
             */
            var cBehavior = _eTarget.Get<Behavior>();
            cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;

            _eTarget.Set(new engine.world.components.Creator(engine.world.components.Creator.CreatorId_Hardcoded));
        });
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