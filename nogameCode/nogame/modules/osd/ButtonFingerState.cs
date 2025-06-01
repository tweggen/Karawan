using System.Numerics;
using engine.news;

namespace nogame.modules.osd;

public class ButtonFingerState : builtin.controllers.AFingerState
{
    public override void HandlePressed(Event ev)
    {
        base.HandlePressed(ev);
    }
    
    
    public ButtonFingerState(in Vector2 pos) : base(in pos)
    {
    }
}