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
        Vector2 v2Total = ev.PhysicalPosition - PressPosition;
        Vector2 v2Now = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;
        v2Total.X *= 16f/9f;
        v2Now.X *= 16f/9f;
        
        v2Total *= _ic.TouchMoveSensitivity;
        v2Now *= _ic.TouchPeakMoveSensitivity;
        v2Total = _ic.TouchSteerTransfer(v2Total);

        if (v2Total.Y < -_ic.ControllerYTolerance)
        {
            /*
             * The user dragged up compare to the press position
             */
            cs.TouchLeftStickUp = (int)(Single.Min(_ic.ControllerYMax, -v2Total.Y-_ic.ControllerYTolerance)
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.TouchLeftStickDown = 0;
        }
        else if (v2Total.Y > _ic.ControllerYTolerance)
        {
            /*
             * The user dragged down compared to the press position.
             */
            cs.TouchLeftStickDown = (int)(Single.Min(_ic.ControllerYMax, v2Total.Y-_ic.ControllerYTolerance) 
                / _ic.ControllerYMax * _ic.TouchAnalogMax);
            cs.TouchLeftStickUp = 0;
        }
#if true
        {

            float moveX = v2Now.X;
            _accuX += moveX;
            float curvedX = _ic.TouchSteerTransferX(_accuX) / 1.8f;
            if (curvedX < -_ic.ControllerXTolerance)
            {
                cs.TouchLeftStickLeft = (int)(Single.Min(_ic.ControllerXMax, -curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickRight = 0;
            }
            else if (curvedX > _ic.ControllerXTolerance)
            {
                cs.TouchLeftStickRight = (int)(Single.Min(_ic.ControllerXMax, curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickLeft = 0;
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
