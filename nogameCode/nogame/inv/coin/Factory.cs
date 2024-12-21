using System;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using static engine.Logger;

namespace nogame.inv.coin;

public class Factory : AModule
{
    private static short _coinMaxDistance = 200;

    public System.Func<Task> CreateAt(Vector3 v3Pos) => new (async () =>
    {
        Model model = await I.Get<ModelCache>().Instantiate(
            "tram1.obj", null, new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            | InstantiateModelParams.ROTATE_Y180
                            | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                MaxDistance = _coinMaxDistance,
            });
        engine.joyce.InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc;

        TaskCompletionSource<DefaultEcs.Entity> tcsEntity = new();
        
        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            // Notice they are not owned by the fragment and hence not removed if the fragment goes away.
            // eTarget.Set(new engine.world.components.Owner(fragmentId));
            eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            eTarget.Set(new engine.behave.components.Behavior(
                    new Behavior())
                { MaxDistance = (short)_coinMaxDistance }
            );
            I.Get<TransformApi>().SetTransform(eTarget, Quaternion.Identity, v3Pos);
            tcsEntity.SetResult(eTarget);
        });

        _engine.QueueEntitySetupAction("nogame.inv.coin", tSetupEntity);
        DefaultEcs.Entity e = await tcsEntity.Task;
    });
}