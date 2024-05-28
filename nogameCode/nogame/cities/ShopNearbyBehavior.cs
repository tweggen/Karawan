using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;

namespace nogame.cities;

public class ShopNearbyBehavior : ABehavior
{
    private Engine _engine;
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;

    public float Distance { get; set; } = 10f;


    private void _onInputButton(Event ev)
    {
        if (ev.Code != "<interact>") return;

        ev.IsHandled = true;

        _engine.QueueEntitySetupAction("shopping", e =>
        {
            var jFountainCubesInstanceDesc = InstanceDesc.CreateFromMatMesh(
                new MatMesh(
                    I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                    engine.joyce.mesh.Tools.CreatePlaneMesh("carcrashfragments", new Vector2(1f, 1f))
                ), 30f
            );
            Vector3 v3Pos;
            v3Pos = EPOI.Get<Transform3ToWorld>().Matrix.Translation;

            e.Set(new engine.behave.components.ParticleEmitter()
            {
                Position = Vector3.Zero,
                ScalePerSec = 1f,
                RandomPos = Vector3.One,
                EmitterTimeToLive = 120,
                Velocity = Vector3.UnitY,
                RotationVelocity = Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    720f / 60f / 180f * Single.Pi),
                ParticleTimeToLive = 300,
                InstanceDesc = jFountainCubesInstanceDesc,
                RandomDirection = 0.5f,
                MaxDistance = 20f,
                CameraMask = 0x00000001,
            });
            e.Set(new engine.joyce.components.Transform3ToWorld()
                {
                    Matrix = Matrix4x4.CreateTranslation(v3Pos),
                    CameraMask = 0x00000001,
                    IsVisible = true
                }
            );
        });

    }


    private float _onInputButtonDistance(Event ev, EmissionContext ectx)
    {
        if (ev.Code != "<interact>") return Single.MinValue;
        
        return (EPOI.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation - ectx.PlayerPos).LengthSquared();
    }
    

    private void _detach()
    {
        if (!_eActionMarker.IsAlive) return;
     
        I.Get<SubscriptionManager>().Unsubscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton);
        _eActionMarker.Dispose();
    }
    
    
    public override void OnDetach(in Entity entity)
    {
        _detach();
    }
    
    
    public override void OutOfRange(in Engine engine0, in Entity entity)
    {
        _detach();
    }

    
    public override void InRange(in Engine engine0, in Entity entity)
    {
        if (_eActionMarker.IsAlive) return;

        _engine = engine0;
        _eActionMarker = engine0.CreateEntity("poi.shop.action");
        _eActionMarker.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 14f), 
            "E to enter", 18, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 2f*Distance });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        I.Get<TransformApi>().SetTransforms(_eActionMarker, true,
            0x00000001, Quaternion.Identity, Vector3.Zero);
        
        I.Get<SubscriptionManager>().Subscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton, _onInputButtonDistance);
    }
}