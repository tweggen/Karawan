using DefaultEcs;
using engine;
using engine.news;

namespace nogame.modules;

public class Gameplay : AModule
{
    private builtin.controllers.FollowCameraController _ctrlFollowCamera;
    private Entity _eCamera;
    private Entity _ePlayer;


    private void _onRootKickoff(Event ev)
    {
        builtin.controllers.FollowCameraController fcc;
        lock (_lo)
        {
            fcc = _ctrlFollowCamera;
        }

        if (null != fcc)
        {
            fcc.ForcePreviousZoomDistance(150f);
        }
    }


    private void _killOldCameraController()
    {
        _ctrlFollowCamera?.DeactivateController();
        _ctrlFollowCamera = null;
    }
    

    private void _onCreateNewCameraController(Entity eCamera, Entity ePlayer)
    {
        _killOldCameraController();

        if (!eCamera.IsAlive || !ePlayer.IsAlive)
        {
            return;
        }
        
        /*
         * Create a camera controller that directly controls the camera with wasd,
         * requires the playerhover.
         */
        _ctrlFollowCamera = new(_engine, eCamera, ePlayer);
        _ctrlFollowCamera.ActivateController();

    }


    private void _onNewCamera(object? sender, Entity eNewCamera)
    {
        Entity eCurrentCamera;
        Entity eCurrentPlayer;
        lock (_lo)
        {
            if (_eCamera == eNewCamera)
            {
                return;
            }

            eCurrentCamera = _eCamera;
            eCurrentPlayer = _ePlayer;
            _eCamera = eNewCamera;
        }
        
        _onCreateNewCameraController(eNewCamera, eCurrentPlayer);
    }


    private void _onNewPlayer(object? sender, Entity eNewPlayer)
    {
        Entity eCurrentCamera;
        Entity eCurrentPlayer;
        lock (_lo)
        {
            if (_ePlayer == eNewPlayer)
            {
                return;
            }

            eCurrentCamera = _eCamera;
            eCurrentPlayer = _ePlayer;
            _ePlayer = eNewPlayer;
        }
        
        _onCreateNewCameraController(eCurrentCamera, eNewPlayer);
    }
    

    public override void ModuleDeactivate()
    {
        _engine.OnCameraEntityChanged -= _onNewCamera;
        _engine.OnPlayerEntityChanged -= _onNewPlayer;
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _engine.RemoveModule(this);
        _killOldCameraController();
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);

        {
            Entity eCamera = _engine.GetCameraEntity();
            Entity ePlayer = _engine.GetPlayerEntity();

            lock (_lo)
            {
                _eCamera = eCamera;
                _ePlayer = ePlayer;
            }
            _onCreateNewCameraController(eCamera, ePlayer);
        }

        I.Get<SubscriptionManager>().Subscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _engine.OnCameraEntityChanged += _onNewCamera;
        _engine.OnPlayerEntityChanged += _onNewPlayer;
    }
}