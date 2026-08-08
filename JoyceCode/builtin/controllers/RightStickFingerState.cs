using System;
using System.Numerics;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

class RightStickFingerState : AInputControllerFingerState
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 v2Now = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;

        v2Now.X *= 16f/9f;
        _ic.V2RightTouchMove += v2Now;

        /*
         * This is the accumulator that feeds FollowCameraController._v2MouseOffseting, so it
         * is the place where an out-of-range touch first becomes visible as motion.
         *
         * The units here are whatever the platform pushed: Wuka.GameSurface normalises to
         * 0..1, so a full-screen swipe is a delta of ~1.0 and a single MOVE step is a few
         * hundredths. A delta anywhere near the hundreds means the producer is sending
         * pixels, which is exactly the SDL2-era assumption to look for.
         *
         * Enable with: debug.category.input = true
         */
        Trace(_dc, $"delta={v2Now} (from {ev.PhysicalPosition}, size {ev.PhysicalSize}) "
                   + $"accumulated={_ic.V2RightTouchMove}");

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