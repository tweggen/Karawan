using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

public abstract class AFingerState : IFingerState
{
    public Vector2 PressPosition;
    public Vector2 LastPosition;
    
    
    public virtual void HandleMotion(Event ev)
    {
        if (LastPosition == default) LastPosition = ev.PhysicalPosition;
        Vector2 vRel = ev.PhysicalPosition - LastPosition;
    }


    public virtual void HandleReleased(Event ev)
    {
    }


    public virtual void HandlePressed(Event ev)
    {
        PressPosition = ev.PhysicalPosition;
        LastPosition = ev.PhysicalPosition;
    }

    
    public AFingerState(
        in Vector2 pos)
    {
        PressPosition = pos;
        LastPosition = pos;
    }
}