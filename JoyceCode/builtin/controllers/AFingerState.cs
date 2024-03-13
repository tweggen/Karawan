using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

abstract class AFingerState
{
    protected readonly InputController _ic;
    public Vector2 PressPosition;
    public Vector2 LastPosition;
    
    public virtual void HandleMotion(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
        if (LastPosition == default) LastPosition = ev.Position;
        Vector2 vRel = ev.Position - LastPosition;
    }

    public virtual void HandleReleased(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
    }

    public virtual void HandlePressed(Event ev)
    {
        var cs = _ic.ControllerState;
        cs.LastInput = DateTime.UtcNow;
    }

    public AFingerState(
        in Vector2 pos,
        InputController ic)
    {
        _ic = ic;
        PressPosition = pos;
        LastPosition = pos;
    }
}