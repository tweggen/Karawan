using System;
using System.Numerics;
using engine;
using engine.news;

namespace builtin.controllers;

class ZoomStickFingerState : AFingerState
{
    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
        var cs = _ic.ControllerState;
        Vector2 vRel = ev.Position - LastPosition;
        LastPosition = ev.Position;
        
        float zoomWay = vRel.Y / _ic.ControllerTouchZoomFull * (8);
                    
        I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
        {
            Position = new Vector2(0f, zoomWay)
        });
    }

        
    public ZoomStickFingerState(
        in Vector2 pos,
        InputController ic) : base(pos, ic)
    {
    }
}