﻿using System;
using System.Collections.Generic;
using DefaultEcs;
using engine;
using engine.news;
using nogame.world;

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

        /*
         * Modules to populate the world after world-building.
         */
        new MyModule<DropCoinModule>()
    };


    private void _onRootKickoff(Event ev)
    {
        M<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

        if (_engine.TryGetCameraEntity(out var eCamera))
        {
            _onNewCamera(this, eCamera);
        }

        if (_engine.TryGetPlayerEntity(out var ePlayer))
        {
            _onNewPlayer(this, ePlayer);
        }
        _engine.OnCameraEntityChanged += _onNewCamera;
        _engine.OnPlayerEntityChanged += _onNewPlayer;
        
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
        if (_isDemoActive)
        {
            _ctrlFollowCamera.CameraDistance = 0.3f;
        }
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
        _engine.OnCameraEntityChanged -= _onNewCamera;
        _engine.OnPlayerEntityChanged -= _onNewPlayer;
        
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