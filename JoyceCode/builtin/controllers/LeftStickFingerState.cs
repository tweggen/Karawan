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
        Vector2 vRel = ev.Position - LastPosition;
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
                
    }
    
    public LeftStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}
