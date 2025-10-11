using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class LeftStickFingerState : AInputControllerFingerState
{
    float _accuX = 0f;

    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        var cs = _ic.ControllerState;
        Vector2 vRel = ev.PhysicalPosition - PressPosition;
        Vector2 vNow = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;
        vRel.X *= 16f/9f;
        vNow.X *= 16f/9f;
        vRel *= _ic.TouchMoveSensitivity;
        vNow *= _ic.TouchPeakMoveSensitivity;
        vRel = _ic.TouchSteerTransfer(vRel);

        if (vRel.Y < -_ic.ControllerYTolerance)
        {
            /*
             * The user dragged up compare to the press position
             */
            cs.AnalogRight2 = (int)(Single.Min(_ic.ControllerYMax, -vRel.Y-_ic.ControllerYTolerance)
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.AnalogLeft2 = 0;
        }
        else if (vRel.Y > _ic.ControllerYTolerance)
        {
            /*
             * The user dragged down compared to the press position.
             */
            cs.AnalogLeft2 = (int)(Single.Min(_ic.ControllerYMax, vRel.Y-_ic.ControllerYTolerance) 
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.AnalogRight2 = 0;
        }
#if true
        {

            float moveX = vNow.X;
            _accuX += moveX;
            float curvedX = _ic.TouchSteerTransferX(_accuX) / 1.8f;
            if (curvedX < -_ic.ControllerXTolerance)
            {
                cs.AnalogLeftStickLeft = (int)(Single.Min(_ic.ControllerXMax, -curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.AnalogLeftStickRight = 0;
            }
            else if (curvedX > _ic.ControllerXTolerance)
            {
                cs.AnalogLeftStickRight = (int)(Single.Min(_ic.ControllerXMax, curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.AnalogLeftStickLeft = 0;
            }
            _accuX *= 0.94f;
        }
#else
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
#endif
        cs.AnalogToWalkControllerNoLock();
    }


    public override void HandleReleased(Event ev)
    {
        base.HandleReleased(ev);
        var cs = _ic.ControllerState;

        cs.AnalogRight2 = 0;
        cs.AnalogLeft2 = 0;
        cs.AnalogLeftStickRight = 0;
        cs.AnalogLeftStickLeft = 0;
        cs.AnalogLeftStickUp = 0;
        cs.AnalogLeftStickDown = 0;
        _accuX = 0f;
        cs.AnalogToWalkControllerNoLock();

        LastPosition = default;
    }


    public override void HandlePressed(Event ev)
    {
        base.HandlePressed(ev);
        _accuX = 0;
    }

    public LeftStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}
