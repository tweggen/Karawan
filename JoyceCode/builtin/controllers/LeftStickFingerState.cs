using System;
using System.Numerics;
using engine.news;

namespace builtin.controllers;

class LeftStickFingerState : AInputControllerFingerState
{
    Vector2 _v2Accu = Vector2.Zero;

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

        /*
         * Straight value Y
         */
        {
            if (v2Total.Y < -_ic.ControllerYTolerance)
            {
                /*
                 * The user dragged up compare to the press position
                 */
                cs.TouchLeftStickUp = (int)(Single.Min(_ic.ControllerYMax, -v2Total.Y - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickDown = 0;
            }
            else if (v2Total.Y > _ic.ControllerYTolerance)
            {
                /*
                 * The user dragged down compared to the press position.
                 */
                cs.TouchLeftStickDown = (int)(Single.Min(_ic.ControllerYMax, v2Total.Y - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickUp = 0;
            }
        }
        
        /*
         * Push Value Y
         */
        {
            float moveY = v2Now.Y;
            _v2Accu.Y += moveY;
            float curvedY = _ic.TouchSteerTransferY(_v2Accu.Y) / 1.8f;
            if (curvedY < -_ic.ControllerYTolerance)
            {
                cs.TouchLeftStickUp = (int)(Single.Min(_ic.ControllerYMax, -curvedY - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickDown = 0;
            }
            else if (curvedY > _ic.ControllerYTolerance)
            {
                cs.TouchLeftStickDown = (int)(Single.Min(_ic.ControllerYMax, curvedY - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickUp = 0;
            }
            _v2Accu.Y *= 0.94f;
        }
        
        /*
         * Straight value X
         */
        {
            if (v2Total.X < -_ic.ControllerXTolerance)
            {
                /*
                 * The user dragged left compare to the press position
                 */
                cs.TouchLeftStickLeft = (int)(Single.Min(_ic.ControllerXMax, -v2Total.X - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickRight = 0;
            }
            else if (v2Total.X > _ic.ControllerXTolerance)
            {
                /*
                 * The user dragged right compared to the press position.
                 */
                cs.TouchLeftStickRight = (int)(Single.Min(_ic.ControllerXMax, v2Total.X - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftStickLeft = 0;
            }
        }

        /*
         * Push value X.
         */
        {
            float moveX = v2Now.X;
            _v2Accu.X += moveX;
            float curvedX = _ic.TouchSteerTransferX(_v2Accu.X) / 1.8f;
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
            _v2Accu.X *= 0.94f;
        }
    }


    public override void HandleReleased(Event ev)
    {
        base.HandleReleased(ev);
        var cs = _ic.ControllerState;

        cs.TouchLeftStickRight = 0;
        cs.TouchLeftStickLeft = 0;
        cs.TouchLeftStickUp = 0;
        cs.TouchLeftStickDown = 0;
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
