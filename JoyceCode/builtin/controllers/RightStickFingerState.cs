using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class RightStickFingerState : AFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 vRel = ev.Position - LastPosition;
        LastPosition = ev.Position;

        _ic.VMouseMove += vRel * 900f * _ic.TouchLookMoveSensitivity;    
    }

    
    public RightStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}