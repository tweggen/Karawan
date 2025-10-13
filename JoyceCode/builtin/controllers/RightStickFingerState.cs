using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class RightStickFingerState : AInputControllerFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 v2Now = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;

        v2Now.X *= 16f/9f;
        _ic.V2RightTouchMove += v2Now;

        //v2Now *= _ic.TouchLookSensitivity;
        //v2Now = _ic.TouchViewTransfer(v2Now) * 4f;
        
        //_ic.V2MouseMove += v2Now * 100f;    
    }


    public RightStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}