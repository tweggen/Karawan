using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.streets;
using engine.world;
using engine.world.components;
using nogame.cities;
using Behavior = engine.behave.components.Behavior;

namespace nogame.quests.HelloFishmonger;


/**
 * The car the player trails in the HelloFishmonger quest: the one place it is built,
 * for a new chase (NarrationBindings) and for one resumed from a save
 * (HelloFishmongerQuestCreator).
 *
 * The car carries no Creator (KAR-475). The quest is what is saved; the car is made
 * again from it. A Creator of its own made EntitySaver store the car by its persistable
 * components alone, which brought it back with no model and no body and with no quest
 * left to chase it.
 */
public static class FishmongerCar
{
    public const string Name = "Fishmonger's car";


    public static ModelCacheParams ModelParams() => new ModelCacheParams()
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
                          | InstantiateModelParams.PHYSICS_CALLBACKS,
            MaxDistance = 150,
            SolidLayerMask = CollisionProperties.Layers.NpcVehicle,
            SensitiveLayerMask = CollisionProperties.Layers.AnyVehicle,
            Name = Name
        }
    };


    public static Task<Model> LoadModelAsync(ModelCacheParams mcp) => I.Get<ModelCache>().LoadModel(mcp);


    /**
     * Create the car driving away from <paramref name="spStart"/>, which must lie in the
     * loaded <paramref name="worldFragment"/>. Logical thread.
     */
    public static Entity CreateLT(Engine engine, Model model, ModelCacheParams mcp, ClusterDesc clusterDesc,
        Fragment worldFragment, StreetPoint spStart)
    {
        var eCar = engine.CreateEntity(Name);

        nogame.characters.car3.CharacterCreator.SetupCharacterMT(
            eCar,
            clusterDesc, worldFragment, spStart,
            model, mcp,
            new nogame.characters.car3.Behavior()
            {
                Navigator = new StreetNavigationController()
                {
                    ClusterDesc = clusterDesc,
                    StartPoint = spStart,
                    Seed = 100,
                    Speed = 35f
                }
            },
            null
        );

        eCar.Set(new EntityName(Name));

        ref var cBehavior = ref eCar.Get<Behavior>();
        cBehavior.Flags |= (ushort)Behavior.BehaviorFlags.MissionCritical;

        return eCar;
    }


    /**
     * The cluster and the junction the car is driving away from, or null if it is not
     * driving a street. A car in its few seconds after a crash answers for the behaviour
     * it will hand back to (IPersistsAs). Logical thread.
     */
    public static (ClusterDesc Cluster, StreetPoint From)? DrivingFrom(Entity eCar)
    {
        if (!eCar.IsAlive || !eCar.Has<Behavior>()) return null;

        object provider = eCar.Get<Behavior>().Provider;
        while (provider is engine.behave.IPersistsAs transient)
        {
            provider = transient.PersistAs;
        }

        if (provider is builtin.tools.SimpleNavigationBehavior { Navigator: StreetNavigationController snc }
            && snc.ClusterDesc != null && snc.StartPoint != null)
        {
            return (snc.ClusterDesc, snc.StartPoint);
        }
        return null;
    }


    /**
     * The junction of <paramref name="clusterDesc"/> closest to <paramref name="v2Local"/>,
     * in the cluster's own frame, as a save recorded it.
     */
    public static StreetPoint? NearestStreetPoint(ClusterDesc clusterDesc, Vector2 v2Local) =>
        clusterDesc.StrokeStore().GetStreetPoints()
            .OrderBy(sp => Vector2.DistanceSquared(sp.Pos, v2Local))
            .FirstOrDefault();
}
