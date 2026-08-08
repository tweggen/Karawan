using System;
using System.Numerics;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

class LeftStickFingerState : AInputControllerFingerState
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    Vector2 _v2Accu = Vector2.Zero;

    /**
     * Throttles the trace below. Motion events arrive at ~100/s during a drag, and one line per
     * event buries everything else in the log.
     */
    private int _nMotions = 0;

    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        var cs = _ic.ControllerState;
        Vector2 v2Raw = ev.PhysicalPosition - PressPosition;
        Vector2 v2Total = v2Raw;
        Vector2 v2Now = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;
        v2Total.X *= 16f/9f;
        v2Now.X *= 16f/9f;

        v2Total *= _ic.TouchMoveSensitivity;
        v2Now *= _ic.TouchPeakMoveSensitivity;
        v2Total = _ic.TouchSteerTransfer(v2Total);

        /*
         * THIS is the path that reaches the ship's physics, as opposed to RightStickFingerState
         * which only moves the camera. Worth its own line with the ranges spelled out, because
         * the numbers are much twitchier than they look:
         *
         *   v2Raw     travel since the press, normalised: 0..1 across the whole surface.
         *   v2Total   v2Raw * TouchMoveSensitivity (4.0), then the steer transfer curve.
         *             ControllerXMax/YMax are 0.2, so |v2Total| >= 0.2 is FULL stick -
         *             i.e. 5% of screen travel already saturates the control.
         *   stick     the resulting analog value, clamped to +/-TouchAnalogMax (255).
         *
         * The stick outputs are clamped, so this path cannot by itself explain an angular
         * runaway - but that is exactly the sort of claim worth being able to check against a
         * log rather than by reading the clamp and assuming.
         */
        if ((++_nMotions & 7) == 0)
        {
            Trace(_dc, $"left stick: raw={v2Raw} (expected 0..1 travel) "
                       + $"-> total={v2Total} (full stick at {_ic.ControllerXMax}, "
                       + $"so {_ic.ControllerXMax / _ic.TouchMoveSensitivity:P0} of the screen) "
                       + $"peak={v2Now} accu={_v2Accu}");
        }

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
                cs.TouchLeftPushUp = (int)(Single.Min(_ic.ControllerYMax, -curvedY - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftPushDown = 0;
            }
            else if (curvedY > _ic.ControllerYTolerance)
            {
                cs.TouchLeftPushDown = (int)(Single.Min(_ic.ControllerYMax, curvedY - _ic.ControllerYTolerance)
                    / _ic.ControllerYMax * _ic.TouchAnalogMax);
                cs.TouchLeftPushUp = 0;
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
                cs.TouchLeftPushLeft = (int)(Single.Min(_ic.ControllerXMax, -curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftPushRight = 0;
            }
            else if (curvedX > _ic.ControllerXTolerance)
            {
                cs.TouchLeftPushRight = (int)(Single.Min(_ic.ControllerXMax, curvedX - _ic.ControllerXTolerance)
                    / _ic.ControllerXMax * _ic.TouchAnalogMax);
                cs.TouchLeftPushLeft = 0;
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
        cs.TouchLeftPushDown = 0;
        cs.TouchLeftPushUp = 0;
        cs.TouchLeftPushLeft = 0;
        cs.TouchLeftPushRight = 0;
        _v2Accu = Vector2.Zero;

        LastPosition = default;
    }


    public override void HandlePressed(Event ev)
    {
        base.HandlePressed(ev);
        _v2Accu = Vector2.Zero;
    }

    public LeftStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}
