using System;
using System.Numerics;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

class RightStickFingerState : AInputControllerFingerState
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    /**
     * One MOVE event cannot legitimately cross more than the whole surface, and positions are
     * normalised to 0..1, so this is the hard upper bound for a well-behaved producer.
     */
    private const float DeltaMax = 1f;

    private const int MaxDeltaReports = 20;
    private static int _nDeltaReports = 0;


    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 v2Now = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;

        v2Now.X *= 16f/9f;
        _ic.V2RightTouchMove += v2Now;

        /*
         * This is the accumulator that feeds FollowCameraController._v2MouseOffseting, so it is
         * where an out-of-range touch first becomes visible as MOVEMENT rather than as a position.
         *
         * EXPECTED RANGES, with the arithmetic that produces them:
         *
         *   delta          one MOVE step. Positions are 0..1, so a full-screen swipe totals 1.0
         *                  and a single step at ~100 events/second is a few hundredths.
         *                  |delta| > 1.0 is impossible for a normalised producer - you cannot
         *                  cross more than the whole surface in one event.
         *   accumulated    drained once per logical frame by GetRightTouchMove, so it is the
         *                  total travel within one frame: <= ~0.2 for a fast swipe at 60 Hz.
         *   degrees        accumulated * FollowCameraController.RIGHT_TOUCH_RELATIVE_AMOUNT (180),
         *                  read directly as degrees of camera rotation. A full-screen swipe is
         *                  therefore 180 degrees, and the pitch clamp is +/-85.
         *
         * A delta in the hundreds means the producer is sending PIXELS - the SDL2-era assumption.
         * That is a Warning, not a Trace, so it shows up without enabling a debug category.
         */
        float degPerUnit = 180f;
        bool deltaInRange = Single.Abs(v2Now.X) <= DeltaMax && Single.Abs(v2Now.Y) <= DeltaMax;

        if (!deltaInRange)
        {
            if (_nDeltaReports < MaxDeltaReports)
            {
                ++_nDeltaReports;
                Warning($"Right-stick touch delta {v2Now} OUT OF RANGE (expected |delta| <= "
                        + $"{DeltaMax}, one screen). pos={ev.PhysicalPosition} size={ev.PhysicalSize}. "
                        + $"That is ~{v2Now.Length() * degPerUnit:F0} degrees of camera rotation from "
                        + $"a single event. Report {_nDeltaReports} of {MaxDeltaReports}.");
            }
        }
        else
        {
            Trace(_dc, $"delta={v2Now} (expected |d| <= {DeltaMax}) "
                       + $"accumulated={_ic.V2RightTouchMove} "
                       + $"-> {_ic.V2RightTouchMove.Length() * degPerUnit:F1} deg this frame "
                       + $"(pitch clamp +/-85); from pos={ev.PhysicalPosition} size={ev.PhysicalSize}");
        }

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