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

    public System.Func<Task> CreateAt(Vector3 v3Pos) => new (async () =>
    {
        Model model = await I.Get<ModelCache>().Instantiate(
            "coin.obj", null, new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            | InstantiateModelParams.ROTATE_Y180
                            | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                MaxDistance = _coinMaxDistance,
            });
        engine.joyce.InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc!;

        TaskCompletionSource<DefaultEcs.Entity> tcsEntity = new();
        
        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            // Notice they are not owned by the fragment and hence not removed if the fragment goes away.
            // eTarget.Set(new engine.world.components.Owner(fragmentId));
            eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            eTarget.Set(new engine.behave.components.Behavior(
                    new Behavior())
                { MaxDistance = (short) jInstanceDesc.MaxDistance }
            );
            I.Get<TransformApi>().SetTransforms(
                eTarget, true, 0x00000001, Quaternion.Identity, v3Pos
                );
            eTarget.Set(new Creator(Creator.CreatorId_Hardcoded));

            StaticHandle staticHandle;   
            engine.physics.Object po;
            lock (_engine.Simulation)
            {
                staticHandle = _engine.Simulation.Statics.Add(
                    new StaticDescription(
                        v3Pos,
                        Quaternion.Identity,
                        _shapeFactory.GetSphereShape(jInstanceDesc.AABBTransformed.Radius)
                    )); 
                po = new(eTarget, staticHandle)
                {
                    CollisionProperties = new CollisionProperties
                    { 
                        Entity = eTarget,
                        Name = "nogame.inv.coin",
                        Flags = 
                            /*CollisionProperties.CollisionFlags.IsTangible 
                            | */ CollisionProperties.CollisionFlags.IsDetectable,
                        LayerMask = 0x0004,
                    }
                };
                po.AddContactListener();
            }
            eTarget.Set(new engine.physics.components.Statics(staticHandle));

            
            tcsEntity.SetResult(eTarget);
        });

        _engine.QueueEntitySetupAction("nogame.inv.coin", tSetupEntity);
        DefaultEcs.Entity e = await tcsEntity.Task;
    });
}