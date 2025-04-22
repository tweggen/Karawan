using System;
using System.Collections.Generic;
using System.Diagnostics;
using builtin.controllers;
using DefaultEcs;
using engine;
using engine.joyce.components;
using engine.news;
using nogame.world;
using static engine.Logger;

namespace nogame.modules;

public class Gameplay : AModule, IInputPart
{
    public float MY_Z_ORDER { get; set; } = 24f;
    
    private builtin.controllers.FollowCameraController _ctrlFollowCamera;

    private Entity _eCurrentCamera;
    private Entity _eCurrentTarget;
    
    private Entity _eDesiredCamera;
    private Entity _eDesiredTarget;

    private Entity _eLatestCamera;
    private Entity _eCurrentPlayer;
    
    private bool _wasEnabled = true;
    private bool _isDemoActive = false;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),
        
        /*
         * Global modules for generic behaviours
         */
        new MyModule<nogame.modules.daynite.FogColor>(),
        
        new MyModule<FollowCameraController>() { ShallActivate = false },

        /*
         * Modules to populate the world after world-building.
         */
        new MyModule<DropCoinModule>()
    };


    private void _onRootKickoff(Event ev)
    {
        M<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

        if (_engine.Camera.TryGet(out var eCamera))
        {
            _onNewCamera(eCamera);
        }

        if (_engine.Player.TryGet(out var ePlayer))
        {
            _onNewPlayer(ePlayer);
        }
        _engine.Camera.AddOnChange(_onNewCamera);
        _engine.Player.AddOnChange(_onNewPlayer);
        
        _engine.OnLogicalFrame += _onLogicalFrame;

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
        bool shallBeEnabled;
        shallBeEnabled = _engine.State == Engine.EngineState.Running;

        if (_wasEnabled != shallBeEnabled)
        {
            _ctrlFollowCamera.EnableInput(shallBeEnabled);
            _wasEnabled = shallBeEnabled;
        }

        if (_isDemoActive
            && (!_eCurrentTarget.IsAlive || !_eCurrentTarget.Has<Transform3ToWorld>() ||
            !_eCurrentTarget.Get<Transform3ToWorld>().IsVisible) )
        {
            Trace($"Changing demo mode subject.");
            _reviewShot();
        }
    }
    

    private void _killOldCameraController()
    {
        if (IsMyModuleActive<FollowCameraController>())
        {
            DeactivateMyModule<FollowCameraController>();
            _eCurrentTarget = default;
            _eCurrentCamera = default;
        }
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
        _ctrlFollowCamera = M<FollowCameraController>();
        if (_isDemoActive)
        {
            _ctrlFollowCamera.CameraDistance = 0.3f;
        }
        
        _ctrlFollowCamera.Target = eCamera;
        _ctrlFollowCamera.Carrot = ePlayer;
        ActivateMyModule<FollowCameraController>();
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


    /**
     * Compute desired camera entity and desired target entity and
     * store it in member variables _desiredCamera and _desiredTarget.
     *
     * This function behaves differently if it is in demo mode or
     * standard gameplay mode:
     *
     * - in demo mode, it picks the _eLatestCamera as desired camersa.
     *   As a carrot, it selects a random entity that is called "car".
     *   If it cannot find any carrot in demo mode, the player's car
     *   is selected as a fallback.
     * - in gameplay mode, also _eLatest is selected as a camera, and
     *   the player entity is selected as a carrot.
     */
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
                            .With<engine.joyce.components.EntityName>()
                            .With<engine.joyce.components.Transform3ToWorld>()
                            .With<engine.physics.components.Body>()
                            .AsEnumerable();
                    DefaultEcs.Entity ePossibleHike = default;
                    foreach (var entity in enumKinematic)
                    {
                        if (entity.Get<engine.joyce.components.EntityName>().Name.Contains("car"))
                        {
                            if (entity.Get<engine.joyce.components.Transform3ToWorld>().IsVisible)
                            {
                                ePossibleHike = entity;
                                break;
                            }
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
    
    
    private void _onNewCamera(Entity eNewCamera)
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


    private void _onNewPlayer(Entity eNewPlayer)
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
                    ev.IsHandled = true;
                    break;
                default:
                    break;
            }
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.Camera.RemoveOnChange(_onNewCamera);
        _engine.Player.RemoveOnChange(_onNewPlayer);
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _engine.RemoveModule(this);
        M<engine.news.InputEventPipeline>().RemoveInputPart(this);
        _killOldCameraController();
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        I.Get<SubscriptionManager>().Subscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        
    }
}