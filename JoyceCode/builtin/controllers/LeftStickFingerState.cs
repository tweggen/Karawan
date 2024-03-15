using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class LeftStickFingerState : AFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        var cs = _ic.ControllerState;
        Vector2 vRel = ev.Position - PressPosition;
        vRel = _ic.TouchSteerTransfer(vRel);
        LastPosition = ev.Position;

        if (vRel.Y < -_ic.ControllerYTolerance)
        {
            /*
             * The user dragged up compare to the press position
             */
            cs.AnalogForward = (int)(Single.Min(_ic.ControllerYMax, -vRel.Y-_ic.ControllerYTolerance)
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.AnalogBackward = 0;
        }
        else if (vRel.Y > _ic.ControllerYTolerance)
        {
            /*
             * The user dragged down compared to the press position.
             */
            cs.AnalogBackward = (int)(Single.Min(_ic.ControllerYMax, vRel.Y-_ic.ControllerYTolerance) 
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.AnalogForward = 0;
        }

        if (vRel.X < -_ic.ControllerXTolerance)
        {
            cs.AnalogLeft = (int)(Single.Min(_ic.ControllerXMax, -vRel.X-_ic.ControllerXTolerance) 
                / _ic.ControllerXMax * _ic.TouchAnalogMax);
            cs.AnalogRight = 0;
        }
        else if (vRel.X > _ic.ControllerXTolerance)
        {
            cs.AnalogRight = (int)(Single.Min(_ic.ControllerXMax, vRel.X-_ic.ControllerXTolerance) 
                / _ic.ControllerXMax * _ic.TouchAnalogMax);
            cs.AnalogLeft = 0;
        }

        cs.AnalogToWalkControllerNoLock();
    }


    public override void HandleReleased(Event ev)
    {
        base.HandleReleased(ev);
        var cs = _ic.ControllerState;

        cs.AnalogForward = 0;
        cs.AnalogBackward = 0;
        cs.AnalogRight = 0;
        cs.AnalogLeft = 0;
        cs.AnalogUp = 0;
        cs.AnalogDown = 0;
        cs.AnalogToWalkControllerNoLock();

        LastPosition = default;
    }
    
    public LeftStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}
