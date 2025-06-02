using System;
using System.Numerics;
using engine;
using engine.news;

namespace builtin.controllers;

class ZoomStickFingerState : AInputControllerFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        Vector2 vRel = ev.PhysicalPosition - LastPosition;
        LastPosition = ev.PhysicalPosition;
        
        float zoomWay = vRel.Y / _ic.ControllerTouchZoomFull * (8);
                    
        I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
        {
            PhysicalPosition = new Vector2(0f, zoomWay)
        });
    }

        
    public ZoomStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}