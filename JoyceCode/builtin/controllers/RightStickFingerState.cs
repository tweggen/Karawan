using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class RightStickFingerState : AInputControllerFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 vRel = ev.PhysicalPosition - LastPosition;
        vRel.X *= 16f/9f;
        vRel *= _ic.TouchLookSensitivity;
        vRel = _ic.TouchViewTransfer(vRel) * 4f;
        
        LastPosition = ev.PhysicalPosition;

        _ic.VMouseMove += vRel * 900f;    
    }

    
    public RightStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}