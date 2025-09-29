using System;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
//using BepuPhysics;
using engine;
using engine.joyce;
using engine.physics;
using engine.world.components;
using static engine.Logger;

namespace nogame.inv.coin;

public class Factory : AModule
{
    private static short _coinMaxDistance = 200;
    private ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

    public System.Func<Task> CreateAt(Vector3 v3Pos) => new(async () =>
    {
        ModelCacheParams mcp = new()
        {
            Url = "coin.obj",
            Params = new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            | InstantiateModelParams.ROTATE_Y180
                            | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                            | InstantiateModelParams.BUILD_PHYSICS
                            | InstantiateModelParams.PHYSICS_STATIC
                            | InstantiateModelParams.PHYSICS_DETECTABLE
                            | InstantiateModelParams.PHYSICS_CALLBACKS,
                MaxDistance = _coinMaxDistance,
            }
        };
        Model model = await I.Get<ModelCache>().LoadModel(mcp);

        TaskCompletionSource<DefaultEcs.Entity> tcsEntity = new();
        
        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            eTarget.Set(new engine.joyce.components.FromModel() { Model = model, ModelCacheParams = mcp });

            // Notice they are not owned by the fragment and hence not removed if the fragment goes away.
            // eTarget.Set(new engine.world.components.Owner(fragmentId));
            eTarget.Set(new engine.behave.components.Behavior(
                    new Behavior())
                { MaxDistance = (short) mcp.Params.MaxVisibilityDistance }
            );
            I.Get<TransformApi>().SetTransforms(
                eTarget, true, 0x00000001, Quaternion.Identity, v3Pos
                );
            eTarget.Set(new Creator(Creator.CreatorId_Hardcoded));

            I.Get<ModelCache>().BuildPerInstance(eTarget, model, mcp);
            
            tcsEntity.SetResult(eTarget);
        });

        _engine.QueueEntitySetupAction("nogame.inv.coin", tSetupEntity);
        DefaultEcs.Entity e = await tcsEntity.Task;
    });
}