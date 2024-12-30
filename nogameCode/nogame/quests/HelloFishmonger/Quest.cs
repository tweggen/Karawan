using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.quest;
using engine.streets;
using engine.world;
using nogame.characters.car3;
using Silk.NET.Core.Native;
using static engine.Logger;
using Behavior = engine.behave.components.Behavior;

namespace nogame.quests.HelloFishmonger;

public class Quest : AModule, IQuest
{
    private ModelCacheParams _mcp;
    private Model _model;
    private bool _isActive = false;

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


    public Description GetDescription()
    {
        return _description;
    }

    
    public bool IsActive()
    {
        return _isActive;
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


    private DefaultEcs.Entity _pickExistingCar()
    {
        /*
         * Randomly chose a destination car.
         */
        DefaultEcs.Entity eVictim = default;
        var listCandidates = _engine.GetEcsWorld().GetEntities()
                .With<engine.behave.components.Behavior>()
                .With<engine.joyce.components.Transform3ToWorld>()
                .AsEnumerable()
                .Where(i => 
                    i.IsAlive
                    && i.Get<Behavior>().Provider != null 
                    && i.Get<Behavior>().Provider.GetType() == typeof(nogame.characters.car3.Behavior))
            ;

        foreach (var e in listCandidates)
        {
            ref var cBehavior = ref e.Get<Behavior>();
            cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;
            eVictim = e;
            break;
        }

        return eVictim;
    }
    
    
    private void _pickActivateCar()
    {
        var eVictim = _pickExistingCar();
        
        if (eVictim == default)
        {
            Error($"No victim found.");
            return;
        }

        _questTarget = new engine.quest.TrailVehicle()
        {
            SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
            MapCameraMask = nogame.modules.map.Module.MapCameraMask,
            ParentEntity = eVictim,
            OnReachTarget = _onReachTarget
        };
        _engine.Run(_questTarget.ModuleActivate);
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
        _engine.TryGetPlayerEntity(out var ePlayer);

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


    private async Task _startQuest()
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
            }
        };
        
        _model = await I.Get<ModelCache>().LoadModel(_mcp);
        
        _selectStartPoint(out var clusterDesc, out var worldFragment, out var streetPoint);
        _engine.QueueEntitySetupAction(CharacterCreator.EntityName, eTarget =>
        {

            /*
             * Create the basic car.
             */
            CharacterCreator.SetupCharacterMT(
                eTarget,
                clusterDesc, worldFragment, streetPoint, 
                _model, _mcp,
                new characters.car3.Behavior(_engine, clusterDesc, streetPoint, 100)
                {
                    Speed = 35f
                },
                null
            );
            
            /*
             * Make it mission critical, so that it is not purged due to unloaded fragments.
             */
            ref var cBehavior = ref eTarget.Get<Behavior>();
            cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;

            /*
             * ... create quest marker and run it.
             */
            _questTarget = new engine.quest.TrailVehicle()
            {
                SensitivePhysicsName = nogame.modules.playerhover.Module.PhysicsName,
                MapCameraMask = nogame.modules.map.Module.MapCameraMask,
                ParentEntity = eTarget,
                OnReachTarget = _onReachTarget
            };
            _engine.Run(_questTarget.ModuleActivate);
        });
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _isActive = true;
        _engine.AddModule(this);

        _engine.Run(_startQuest);
    }
}