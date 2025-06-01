using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

abstract class AInputControllerFingerState : AFingerState
{
    protected readonly InputController _ic;

    public virtual void HandleMotion(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
        base.HandleMotion(ev);
    }


    public virtual void HandleReleased(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
        base.HandleReleased(ev);
    }

    
    public virtual void HandlePressed(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
        base.HandlePressed(ev);
    }


    public AInputControllerFingerState(
        in Vector2 pos,
        InputController ic) : base(pos)
    {
        _ic = ic;
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
    }
}