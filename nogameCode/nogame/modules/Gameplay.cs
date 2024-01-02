using System;
using System.Collections.Generic;
using DefaultEcs;
using engine;
using engine.news;

namespace nogame.modules;

public class Gameplay : AModule, IInputPart
{
    public static float MY_Z_ORDER = 24f;

    private builtin.controllers.FollowCameraController _ctrlFollowCamera;

    private Entity _eCurrentCamera;
    private Entity _eCurrentTarget;
    
    private Entity _eDesiredCamera;
    private Entity _eDesiredTarget;

    private Entity _eLatestCamera;
    private Entity _eCurrentPlayer;
    
    private bool _wasEnabled = true;
    private bool _isDemoActive = false;

    
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


    private void _onLogicalFrame(object? sender, float dt)
    {
        // TXWTODO: Remove this workaround. We still need a smart idea, who can read the analog controls.
        var frontZ = I.Get<InputEventPipeline>().GetFrontZ();
        bool shallBeEnabled;
        if (frontZ != nogame.modules.playerhover.WASDPhysics.MY_Z_ORDER)
        {
            shallBeEnabled = false;
        }
        else
        {
            shallBeEnabled = true;
        }

        if (_wasEnabled != shallBeEnabled)
        {
            _ctrlFollowCamera.EnableInput(shallBeEnabled);
            _wasEnabled = shallBeEnabled;
        }
    }
    

    private void _killOldCameraController()
    {
        _ctrlFollowCamera?.DeactivateController();
        _ctrlFollowCamera = null;
        _eCurrentTarget = default;
        _eCurrentCamera = default;
    }
    

    private void _createFollowCameraController(Entity eCamera, Entity ePlayer)
    {
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


    private void _createDesiredCameraController()
    {
        Entity eDesiredCamera;
        Entity eDesiredTarget;

        lock (_lo)
        {
            eDesiredCamera = _eDesiredCamera;
            eDesiredTarget = _eDesiredTarget;
            
            if (!eDesiredCamera.IsAlive || !eDesiredTarget.IsAlive)
            {
                return;
            }

            _eCurrentCamera = _eDesiredCamera;
            _eCurrentTarget = _eDesiredTarget;
        }


        _createFollowCameraController(eDesiredCamera, eDesiredTarget);
    }


    private bool _computeDesiredCamera()
    {
        /*
         * Currently, we unconditionally take the current camera and player as desired
         * camera.
         */
        lock (_lo)
        {
            Entity eDesiredCamera;
            Entity eDesiredTarget;
            if (_isDemoActive)
            {
                eDesiredCamera = _eLatestCamera;
                try
                {
                    IEnumerable<Entity> enumKinematic =
                        _engine.GetEcsWorld().GetEntities()
                            .With<engine.behave.components.Behavior>()
                            .With<engine.audio.components.MovingSound>()
                            .With<engine.joyce.components.Transform3ToWorld>()
                            .With<engine.physics.components.Kinetic>()
                            .AsEnumerable();
                    DefaultEcs.Entity ePossibleHike = default;
                    foreach (var entity in enumKinematic)
                    {
                        if (entity.Get<engine.joyce.components.Transform3ToWorld>().IsVisible)
                        {
                            ePossibleHike = entity;
                            break;
                        }
                    }
                    if (ePossibleHike == default)
                    {
                        eDesiredTarget = _eCurrentPlayer;
                    }
                    else
                    {
                        eDesiredTarget = ePossibleHike;
                    }
                }
                catch (Exception e)
                {
                    eDesiredTarget = _eCurrentPlayer;
                }
            }
            else
            {
                eDesiredCamera = _eLatestCamera;
                eDesiredTarget = _eCurrentPlayer;
            }
            
            if (!eDesiredCamera.IsAlive || !eDesiredTarget.IsAlive)
            {
                return false;
            }

            _eDesiredCamera = eDesiredCamera;
            _eDesiredTarget = eDesiredTarget;

            if (_eCurrentCamera != _eDesiredCamera || _eDesiredTarget != _eCurrentTarget)
            {
                return true;
            }
        }

        return false;
    }


    private void _reviewShot()
    {
        if (_computeDesiredCamera())
        {
            _killOldCameraController();
            _createDesiredCameraController();
        }
    }
    
    
    private void _onNewCamera(object? sender, Entity eNewCamera)
    {
        lock (_lo)
        {
            if (_eLatestCamera == eNewCamera)
            {
                return;
            }

            _eLatestCamera = eNewCamera;
        }

        _reviewShot();
    }


    private void _onNewPlayer(object? sender, Entity eNewPlayer)
    {
        lock (_lo)
        {
            if (_eCurrentPlayer== eNewPlayer)
            {
                return;
            }
            _eCurrentPlayer = eNewPlayer;
        }

        _reviewShot();
    }


    private void _toggleDemo()
    {
        lock (_lo)
        {
            _isDemoActive = !_isDemoActive;
        }
        _reviewShot();
    }
    
    
    public void InputPartOnInputEvent(engine.news.Event ev)
    {
        if (ev.Type == Event.INPUT_KEY_PRESSED)
        {
            switch (ev.Code)
            {
                case "(F10)":
                    _toggleDemo();
                    break;
                default:
                    break;
            }
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.OnCameraEntityChanged -= _onNewCamera;
        _engine.OnPlayerEntityChanged -= _onNewPlayer;
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _engine.RemoveModule(this);
        I.Get<engine.news.InputEventPipeline>().RemoveInputPart(this);
        _killOldCameraController();
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        I.Get<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _engine.AddModule(this);

        {
            Entity eCamera = _engine.GetCameraEntity();
            Entity ePlayer = _engine.GetPlayerEntity();

            lock (_lo)
            {
                _eLatestCamera = eCamera;
                _eCurrentPlayer = ePlayer;
            }

            _reviewShot();
        }

        I.Get<SubscriptionManager>().Subscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _engine.OnCameraEntityChanged += _onNewCamera;
        _engine.OnPlayerEntityChanged += _onNewPlayer;
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}