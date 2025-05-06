using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using engine;
using engine.gongzuo;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.world;

namespace nogame.modules.playerhover;

public class DriveCarCollisionsModule : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
        new MyModule<nogame.modules.playerhover.UpdateEmissionContext>()
    };


    private Boom.ISoundAPI _aSound;
    
    private PlingPlayer _plingPlayer = new();
    
    private Boom.ISound _polyballSound;
    private Boom.ISound _soundCrash = null;

    private DefaultEcs.Entity _ePlayer;
    private Vector3 _v3Player;
    
    private void _decreaseHealth(int less)
    {
        lock (_lo)
        {
            var gameState = M<AutoSave>().GameState;
            gameState.Health = int.Max(0, gameState.Health - less);
        }
    }


    private void _playCollisionSound()
    {
        _soundCrash.Stop();
        _soundCrash.Volume = 0.1f;
        _soundCrash.Play();
    }


    private void _createCollisionParticles(ContactEvent cev)
    {
        _engine.QueueEntitySetupAction("carcollision", e =>
        {
            if (default == _ePlayer)
            {
                return;
            }
            
            var jFountainCubesInstanceDesc = InstanceDesc.CreateFromMatMesh(
                new MatMesh(
                    I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                    engine.joyce.mesh.Tools.CreatePlaneMesh("carcrashfragments", new Vector2(0.1f,0.1f))
                ), 20f
            );
            Vector3 v3Pos;
            lock (_engine.Simulation)
            {
                v3Pos = _v3Player;
            }

            v3Pos += cev.ContactInfo.ContactOffset;
            e.Set(new engine.behave.components.ParticleEmitter()
            {
                Position = Vector3.Zero,
                ScalePerSec = 1f,
                RandomPos = Vector3.One,
                EmitterTimeToLive = 10,
                Velocity = 3f * cev.ContactInfo.ContactNormal,
                ParticleTimeToLive = 30,
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
    

    private void _onAnonymousCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);
        _playCollisionSound();
        _decreaseHealth(14);
    }


    private void _onPolytopeCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        cev.ContactInfo.PropertiesB.Entity.Set(
            new engine.behave.components.Behavior(new nogame.cities.PolytopeVanishBehaviour() { Engine = _engine }));
        var gameState = M<AutoSave>().GameState;
        gameState.NumberPolytopes++;
        gameState.Health = 1000;

        _polyballSound.Stop();
        _polyballSound.Play();
    }


    private void _onCubeCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);

        cev.ContactInfo.PropertiesB.Entity.Set(
            new engine.behave.components.Behavior(new nogame.characters.cubes.CubeVanishBehavior()
                { Engine = _engine }));

        _plingPlayer.PlayPling();
        _plingPlayer.Next();

        var gameState = M<AutoSave>().GameState;
        gameState.NumberCubes++;
    }


    private void _onCarCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);

        var other = cev.ContactInfo.PropertiesB;

        _playCollisionSound();
        
        // TXWTODO: This shall be dependent on the person you are in the moment.
        _decreaseHealth(17);
    }
    
    
    private void _onPlayerEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_ePlayer != entity)
            {
                _ePlayer = entity;
                isChanged = true;
            }
        }
    }

    
    private void _onLogicalFrame(object? sender, float dt)
    {
        if (_ePlayer == default)
        {
            return;
        }

        if (_ePlayer.Has<Transform3ToWorld>())
        {
            ref var cTransform3ToWorld = ref _ePlayer.Get<Transform3ToWorld>();
            _v3Player = cTransform3ToWorld.Matrix.Translation;
        }
    }

    
    public override void Dispose()
    {
        _soundCrash.Dispose();
        _soundCrash = null;
    }


    protected override void OnModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _engine.Player.RemoveOnChange(_onPlayerEntityChanged);
        
        I.Get<SubscriptionManager>().Unsubscribe(
            HoverBehavior.PLAYER_COLLISION_ANONYMOUS, _onAnonymousCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            HoverBehavior.PLAYER_COLLISION_CUBE, _onCubeCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            HoverBehavior.PLAYER_COLLISION_CAR3, _onCarCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            HoverBehavior.PLAYER_COLLISION_POLYTOPE, _onPolytopeCollision);
    }


    private async Task _setupModule()
    {
        _aSound = I.Get<Boom.ISoundAPI>();
        if (null == _soundCrash)
        {
            _soundCrash = _aSound.FindSound($"car-collision.ogg");
        }

        {
            _polyballSound = _aSound.FindSound("polyball.ogg");
            _polyballSound.Volume = 0.03f;
        }
    }


    protected override void OnModuleActivate()
    {
        I.Get<SubscriptionManager>().Subscribe(
            HoverBehavior.PLAYER_COLLISION_ANONYMOUS, _onAnonymousCollision);
        I.Get<SubscriptionManager>().Subscribe(
            HoverBehavior.PLAYER_COLLISION_CUBE, _onCubeCollision);
        I.Get<SubscriptionManager>().Subscribe(
            HoverBehavior.PLAYER_COLLISION_CAR3, _onCarCollision);
        I.Get<SubscriptionManager>().Subscribe(
            HoverBehavior.PLAYER_COLLISION_POLYTOPE, _onPolytopeCollision);
        
        _engine.Camera.AddOnChange(_onPlayerEntityChanged);
        
        _engine.OnLogicalFrame += _onLogicalFrame;

        _engine.Run(_setupModule);
    }
}